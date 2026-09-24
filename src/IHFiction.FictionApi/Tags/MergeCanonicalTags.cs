using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Tags;

internal sealed partial class MergeCanonicalTags(
    FictionDbContext context,
    ILogger<MergeCanonicalTags> logger) : IUseCase, INameEndpoint<MergeCanonicalTags>
{
    internal static class Errors
    {
        public static readonly DomainError NotFound = new("MergeCanonicalTags.NotFound", "One or more canonical tags were not found.");
        public static readonly DomainError InvalidSelection = new("MergeCanonicalTags.InvalidSelection", "Select a canonical tag and at least one distinct source tag.");
        public static readonly DomainError InvalidReason = new("MergeCanonicalTags.InvalidReason", "A meaningful administrative reason is required.");
    }

    internal sealed record MergeCanonicalTagsBody(
        Ulid CanonicalTagId,
        [property: Required, MinLength(1)] string[] SourceTagIds,
        [property: Required, StringLength(500, MinimumLength = 3)] string Reason);

    internal sealed record MergeCanonicalTagsResponse(
        Ulid CanonicalTagId,
        string DisplayFormat,
        IReadOnlyCollection<string> Synonyms,
        int WorkCount,
        int MergedTagCount);

    public async Task<Result<MergeCanonicalTagsResponse>> HandleAsync(
        MergeCanonicalTagsBody body,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body.Reason) || body.Reason.Trim().Length < 3) return Errors.InvalidReason;
        if (body.SourceTagIds is null || body.SourceTagIds.Length == 0) return Errors.InvalidSelection;

        var parsedSourceIds = new List<Ulid>(body.SourceTagIds.Length);
        foreach (var sourceTagId in body.SourceTagIds)
        {
            if (!Ulid.TryParse(sourceTagId, out var parsedSourceId)) return Errors.InvalidSelection;
            parsedSourceIds.Add(parsedSourceId);
        }

        var sourceIds = parsedSourceIds.Distinct().ToArray();
        if (sourceIds.Length != body.SourceTagIds.Length || sourceIds.Contains(body.CanonicalTagId)) return Errors.InvalidSelection;

        var requestedIds = sourceIds.Append(body.CanonicalTagId).ToArray();
        var existingIds = await context.Tags.OfType<CanonicalTag>()
            .Where(tag => requestedIds.Contains(tag.Id))
            .Select(tag => tag.Id)
            .ToListAsync(cancellationToken);
        if (existingIds.Count != requestedIds.Length) return Errors.NotFound;

        MergeCanonicalTagsResponse? response = null;
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            context.ChangeTracker.Clear();
            var tags = await context.Tags.OfType<CanonicalTag>()
                .Where(tag => requestedIds.Contains(tag.Id))
                .Include(tag => tag.Synonyms)
                .Include(tag => tag.Works)
                .ToListAsync(transactionCancellationToken);
            var target = tags.Single(tag => tag.Id == body.CanonicalTagId);
            var sources = tags.Where(tag => tag.Id != target.Id).ToArray();
            var familyKeys = target.Synonyms.Select(synonym => synonym.NormalizedKey)
                .Append(target.NormalizedKey)
                .ToHashSet(StringComparer.Ordinal);
            var sourceSpellings = sources
                .Select(source => (source.Category, source.Subcategory, source.Value, source.NormalizedKey))
                .ToArray();

            foreach (var source in sources)
            {
                foreach (var work in source.Works.ToArray())
                {
                    if (!target.Works.Any(candidate => candidate.Id == work.Id)) target.Works.Add(work);
                    work.Tags.Remove(source);
                }

                foreach (var synonym in source.Synonyms.ToArray())
                {
                    if (familyKeys.Add(synonym.NormalizedKey))
                    {
                        synonym.CanonicalTag = target;
                        synonym.CanonicalTagId = target.Id;
                    }
                    else
                    {
                        context.Tags.Remove(synonym);
                    }
                }
            }

            await context.SaveChangesAsync(acceptAllChangesOnSuccess: false, transactionCancellationToken);
            context.ChangeTracker.AcceptAllChanges();

            context.Tags.RemoveRange(sources);
            await context.SaveChangesAsync(acceptAllChangesOnSuccess: false, transactionCancellationToken);
            context.ChangeTracker.AcceptAllChanges();

            foreach (var spelling in sourceSpellings)
            {
                if (!familyKeys.Add(spelling.NormalizedKey)) continue;
                context.Tags.Add(Tag.CreateSynonym(target, spelling.Category, spelling.Subcategory, spelling.Value));
            }

            await context.SaveChangesAsync(acceptAllChangesOnSuccess: false, transactionCancellationToken);
            context.ChangeTracker.AcceptAllChanges();

            context.ChangeTracker.Clear();
            var persistedTarget = await context.Tags.OfType<CanonicalTag>()
                .Include(tag => tag.Synonyms)
                .Include(tag => tag.Works)
                .SingleAsync(tag => tag.Id == body.CanonicalTagId, transactionCancellationToken);
            response = new MergeCanonicalTagsResponse(
                persistedTarget.Id,
                persistedTarget.ToString(),
                [.. persistedTarget.Synonyms.Select(synonym => synonym.ToString())],
                persistedTarget.Works.Count,
                sourceIds.Length);
        }, async transactionCancellationToken =>
            !await context.Tags.OfType<CanonicalTag>().AnyAsync(tag => sourceIds.Contains(tag.Id), transactionCancellationToken)
            && await context.Tags.OfType<CanonicalTag>().AnyAsync(tag => tag.Id == body.CanonicalTagId, transactionCancellationToken),
            cancellationToken);

        context.ChangeTracker.AcceptAllChanges();

        LogMerged(
            principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
            string.Join(',', sourceIds),
            body.CanonicalTagId,
            body.Reason.Trim());

        return response!;
    }

    [LoggerMessage(LogLevel.Information, "Administrator {AdministratorId} merged canonical tags {SourceTagIds} into {CanonicalTagId}. Reason: {Reason}")]
    private partial void LogMerged(string administratorId, string sourceTagIds, Ulid canonicalTagId, string reason);

    public static string EndpointName => nameof(MergeCanonicalTags);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder) => builder
            .MapPost("admin/tags/merge", async (
                [FromBody] MergeCanonicalTagsBody body,
                MergeCanonicalTags useCase,
                ClaimsPrincipal principal,
                CancellationToken cancellationToken) =>
                (await useCase.HandleAsync(body, principal, cancellationToken)).ToOkResult())
            .WithSummary("Merge Canonical Tags")
            .WithDescription("Atomically merges one or more source canonical families into a selected canonical family, preserving alternate spellings as synonyms. Requires an administrative reason.")
            .WithTags(ApiTags.Tags.Administration)
            .RequireAuthorization("admin")
            .WithStandardResponses()
            .Produces<MergeCanonicalTagsResponse>();
    }
}
