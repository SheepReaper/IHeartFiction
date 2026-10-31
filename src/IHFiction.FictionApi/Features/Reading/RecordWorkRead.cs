#pragma warning disable CA1515 // Wolverine discovers public messages and handlers.

using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Infrastructure;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using Wolverine;

namespace IHFiction.FictionApi.Features.Reading;

// Preserve the durable message identity across folder/namespace changes.
[Wolverine.Attributes.MessageIdentity("IHFiction.FictionApi.Stories.RecordWorkReadRequested")]
public sealed record RecordWorkReadRequested(
    Ulid WorkId,
    Guid? AuthenticatedUserId,
    string? DeviceId,
    DateTime QualifiedAt = default);

internal sealed class RecordWorkRead(IMessageBus messageBus, TimeProvider timeProvider) : IUseCase, INameEndpoint<RecordWorkRead>
{
    internal sealed record RecordWorkReadBody(
        [property: Range(10, 3600)] int ActiveSeconds,
        bool HasMeaningfulInteraction);

    internal sealed record RecordWorkReadResponse(Ulid WorkId, bool Qualified);

    internal static class Errors
    {
        public static readonly DomainError NotQualified = new("RecordWorkRead.NotQualified", "The reading session has not met the qualification threshold.");
        public static readonly DomainError DeviceRequired = new("RecordWorkRead.DeviceRequired", "A valid device identifier is required for anonymous readers.");
        public static readonly DomainError NotDirectlyReadable = new("RecordWorkRead.NotDirectlyReadable", "The work is not directly readable.");
        public static readonly DomainError WorkNotFound = new("RecordWorkRead.NotFound", "Work not found.");
    }

    public async Task<Result<RecordWorkReadResponse>> HandleAsync(
        Ulid id,
        RecordWorkReadBody body,
        ClaimsPrincipal principal,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (body.ActiveSeconds < 10 || !body.HasMeaningfulInteraction) return Errors.NotQualified;

        Guid? authenticatedUserId = null;
        if (principal.Identity?.IsAuthenticated == true)
        {
            var userIdResult = principal.GetUid();
            if (userIdResult.IsFailure) return userIdResult.DomainError;
            authenticatedUserId = userIdResult.Value;
        }

        if (authenticatedUserId is null && !DeviceIdHeader.IsValid(deviceId)) return Errors.DeviceRequired;
        if (deviceId is not null && !DeviceIdHeader.IsValid(deviceId)) return CommonErrors.Device.InvalidIdentifier;

        await messageBus.PublishAsync(new RecordWorkReadRequested(
            id,
            authenticatedUserId,
            deviceId,
            timeProvider.GetUtcNow().UtcDateTime));

        return new RecordWorkReadResponse(id, true);
    }

    public static string EndpointName => nameof(RecordWorkRead);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder) => builder
            .MapPost("works/{id:ulid}/reads", async (
                [FromRoute] Ulid id,
                [FromBody] RecordWorkReadBody body,
                [FromHeader(Name = DeviceIdHeader.Name)] string? deviceId,
                ClaimsPrincipal principal,
                RecordWorkRead useCase,
                CancellationToken cancellationToken) =>
                (await useCase.HandleAsync(id, body, principal, deviceId, cancellationToken))
                    .ToResult(accepted => Results.Accepted(value: accepted)))
            .WithSummary("Record a qualified work read")
            .WithDescription("Durably queues a qualified unique read for asynchronous recording against a story or chapter and its parent works.")
            .WithTags(ApiTags.Works)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false)
            .RequireRateLimiting(RateLimitPolicies.QualifiedReads)
            .Produces<RecordWorkReadResponse>(StatusCodes.Status202Accepted);
    }
}

public sealed partial class RecordWorkReadHandler(
    FictionDbContext context,
    TimeProvider timeProvider,
    ILogger<RecordWorkReadHandler>? logger = null)
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Debug,
        Message = "Discarding queued read for work {WorkId}: {Reason}")]
    private partial void LogDiscardedRead(Ulid workId, string reason);

    public async Task Handle(RecordWorkReadRequested message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.AuthenticatedUserId is null && !DeviceIdHeader.IsValid(message.DeviceId))
        {
            if (logger is not null) LogDiscardedRead(message.WorkId, RecordWorkRead.Errors.DeviceRequired.Code);
            return;
        }

        var hierarchy = await LoadHierarchyAsync(message.WorkId, cancellationToken);
        if (hierarchy.IsFailure)
        {
            if (logger is not null) LogDiscardedRead(message.WorkId, hierarchy.DomainError.Code);
            return;
        }

        var now = message.QualifiedAt == default
            ? timeProvider.GetUtcNow().UtcDateTime
            : message.QualifiedAt;
        var userKey = message.AuthenticatedUserId is { } userId ? ReadIdentity.ForUser(userId) : null;
        var deviceKey = message.DeviceId is not null ? ReadIdentity.ForDevice(message.DeviceId) : null;
        var readerKey = userKey ?? deviceKey!;
        var isCounted = message.AuthenticatedUserId is null
            || !hierarchy.Value.AuthorUserIds.Contains(message.AuthenticatedUserId.Value);
        var workIds = hierarchy.Value.Works.Select(work => work.Id).ToArray();
        var strategy = context.Database.CreateExecutionStrategy();

        try
        {
            await strategy.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                Dictionary<Ulid, int> countDeltas = [];
                if (userKey is not null && deviceKey is not null)
                {
                    await MergeDeviceReadsAsync(
                        deviceKey,
                        userKey,
                        message.AuthenticatedUserId!.Value,
                        now,
                        countDeltas,
                        transactionCancellationToken);
                }

                foreach (var workId in workIds)
                {
                    var existing = await context.WorkReads.FirstOrDefaultAsync(
                        read => read.WorkId == workId && read.ReaderKey == readerKey,
                        transactionCancellationToken);

                    if (existing is not null)
                    {
                        existing.LastReadAt = now > existing.LastReadAt ? now : existing.LastReadAt;
                        continue;
                    }

                    context.WorkReads.Add(new WorkRead
                    {
                        WorkId = workId,
                        ReaderKey = readerKey,
                        IsCounted = isCounted,
                        FirstReadAt = now,
                        LastReadAt = now
                    });

                    if (isCounted) AddCountDelta(countDeltas, workId, 1);
                }

                await context.SaveChangesAsync(acceptAllChangesOnSuccess: false, transactionCancellationToken);
                foreach (var (workId, delta) in countDeltas.Where(entry => entry.Value != 0))
                {
                    await context.Works
                        .Where(work => work.Id == workId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(
                            work => work.ReadCount,
                            work => work.ReadCount + delta), transactionCancellationToken);
                }
            }, async verificationCancellationToken =>
                await context.WorkReads
                    .AsNoTracking()
                    .CountAsync(
                        read => workIds.Contains(read.WorkId) && read.ReaderKey == readerKey,
                        verificationCancellationToken) == workIds.Length,
                cancellationToken);

            context.ChangeTracker.AcceptAllChanges();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.ChangeTracker.Clear();
            // Another delivery inserted the same (work, reader) rows. The command is idempotent.
        }
    }

    private async Task<Result<ReadHierarchy>> LoadHierarchyAsync(Ulid id, CancellationToken cancellationToken)
    {
        var story = await context.Stories
            .Include(candidate => candidate.Authors)
            .Include(candidate => candidate.Owner)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (story is not null)
        {
            var hasChildren = await context.Chapters.AnyAsync(chapter => chapter.StoryId == story.Id, cancellationToken)
                || await context.Books.AnyAsync(book => book.StoryId == story.Id, cancellationToken);
            if (!story.IsPublished) return CommonErrors.Story.NotPublished;
            if (!story.HasContent || hasChildren) return RecordWorkRead.Errors.NotDirectlyReadable;
            return new ReadHierarchy([story], AuthorIds(story));
        }

        var chapter = await context.Chapters
            .Include(candidate => candidate.Book)
            .Include(candidate => candidate.Story)
                .ThenInclude(candidate => candidate!.Authors)
            .Include(candidate => candidate.Story)
                .ThenInclude(candidate => candidate!.Owner)
            .Include(candidate => candidate.Book)
                .ThenInclude(candidate => candidate!.Story)
                    .ThenInclude(candidate => candidate.Authors)
            .Include(candidate => candidate.Book)
                .ThenInclude(candidate => candidate!.Story)
                    .ThenInclude(candidate => candidate.Owner)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (chapter is null) return RecordWorkRead.Errors.WorkNotFound;
        var parentStory = chapter.Story ?? chapter.Book?.Story;
        if (parentStory is null) return CommonErrors.Story.NotFound;
        if (!parentStory.IsPublished) return CommonErrors.Story.NotPublished;
        if (!chapter.IsPublished) return CommonErrors.Chapter.NotPublished;

        List<Work> works = [chapter];
        if (chapter.Book is { IsPublished: true } book) works.Add(book);
        works.Add(parentStory);
        return new ReadHierarchy(works, AuthorIds(parentStory));
    }

    private async Task MergeDeviceReadsAsync(
        string deviceKey,
        string userKey,
        Guid userId,
        DateTime now,
        Dictionary<Ulid, int> countDeltas,
        CancellationToken cancellationToken)
    {
        var deviceReads = await context.WorkReads
            .Include(read => read.Work)
            .Where(read => read.ReaderKey == deviceKey)
            .ToListAsync(cancellationToken);

        foreach (var deviceRead in deviceReads)
        {
            var isAuthorRead = await IsStoryAuthorForWorkAsync(deviceRead.WorkId, userId, cancellationToken);
            var userRead = await context.WorkReads.FirstOrDefaultAsync(
                read => read.WorkId == deviceRead.WorkId && read.ReaderKey == userKey,
                cancellationToken);
            if (userRead is null)
            {
                deviceRead.ReaderKey = userKey;
                deviceRead.LastReadAt = now > deviceRead.LastReadAt ? now : deviceRead.LastReadAt;
                if (isAuthorRead && deviceRead.IsCounted)
                {
                    deviceRead.IsCounted = false;
                    AddCountDelta(countDeltas, deviceRead.WorkId, -1);
                }
            }
            else
            {
                userRead.FirstReadAt = userRead.FirstReadAt < deviceRead.FirstReadAt ? userRead.FirstReadAt : deviceRead.FirstReadAt;
                userRead.LastReadAt = now > userRead.LastReadAt ? now : userRead.LastReadAt;
                var mergedIsCounted = !isAuthorRead && (deviceRead.IsCounted || userRead.IsCounted);
                var previousContributions = (deviceRead.IsCounted ? 1 : 0) + (userRead.IsCounted ? 1 : 0);
                AddCountDelta(countDeltas, deviceRead.WorkId, -(previousContributions - (mergedIsCounted ? 1 : 0)));
                userRead.IsCounted = mergedIsCounted;
                context.WorkReads.Remove(deviceRead);
            }
        }
    }

    private static void AddCountDelta(Dictionary<Ulid, int> deltas, Ulid workId, int delta) =>
        deltas[workId] = deltas.GetValueOrDefault(workId) + delta;

    private Task<bool> IsStoryAuthorForWorkAsync(Ulid workId, Guid userId, CancellationToken cancellationToken) =>
        context.Stories.AnyAsync(story =>
            (story.Id == workId
                || story.Books.Any(book => book.Id == workId || book.Chapters.Any(chapter => chapter.Id == workId))
                || story.Chapters.Any(chapter => chapter.Id == workId))
            && (story.Owner.UserId == userId || story.Authors.Any(author => author.UserId == userId)),
            cancellationToken);

    private static HashSet<Guid> AuthorIds(Story story) =>
    [
        .. story.Authors
                .Select(author => author.UserId)
,
        story.Owner.UserId,
    ];

    private sealed record ReadHierarchy(IReadOnlyList<Work> Works, HashSet<Guid> AuthorUserIds);
}
