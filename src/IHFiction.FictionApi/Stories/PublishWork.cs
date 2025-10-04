using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Authors;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Stories;

internal sealed class PublishWork(
    FictionDbContext context,
    UserService userService,
    TimeProvider dateTimeProvider) : IUseCase, INameEndpoint<PublishWork>
{
    internal static class Errors
    {
        public static readonly DomainError WorkNotFound = new("PublishWork.WorkNotFound", "Work not found.");
        public static readonly DomainError AuthorNotFound = CommonErrors.Author.NotRegistered;
        public static readonly DomainError NotAuthorized = CommonErrors.Author.NotAuthorized;
        public static readonly DomainError ConcurrencyConflict = CommonErrors.Database.ConcurrencyConflict;
        public static readonly DomainError DatabaseError = CommonErrors.Database.SaveFailed;
        public static readonly DomainError AlreadyPublished = new("PublishWork.AlreadyPublished", "Work is already published.");
        public static readonly DomainError NoContentToPublish = new("PublishWork.NoContentToPublish", "Work has no content to publish.");
        public static readonly DomainError OnlyOwnerCanPublish = new("PublishWork.OnlyOwnerCanPublish", "Only the work owner can publish the work.");
    }

    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<PublishWorkResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Request model for publishing a work.
    /// </summary>
    /// <param name="PublishAll">Indicates whether to publish all child works.</param>
    internal sealed record PublishWorkBody(bool PublishAll = false);

    /// <summary>
    /// Response model for publishing a work.
    /// </summary>
    /// <param name="WorkId">The unique identifier of the published work.</param>
    /// <param name="Title">The title of the published work.</param>
    /// <param name="Type">The type of the published work.</param>
    /// <param name="PublishedAt">The timestamp when the work was published.</param>
    /// <param name="UpdatedAt">The timestamp when the work was last updated.</param>
    /// <param name="HasContent">Indicates whether the work has content.</param>
    /// <param name="HasChildren">Indicates whether the work has child works.</param>
    /// <param name="ChildCount">The count of child works.</param>
    internal sealed record PublishWorkResponse(
        Ulid WorkId,
        string Title,
        string Type,
        DateTime PublishedAt,
        DateTime UpdatedAt,
        bool HasContent,
        bool HasChildren,
        int ChildCount);

    public async Task<Result<PublishWorkResponse>> HandleAsync(
        Ulid id,
        PublishWorkBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var authorResult = await userService.GetAuthorAsync(claimsPrincipal, cancellationToken);
        if (authorResult.IsFailure) return Errors.AuthorNotFound;
        var author = authorResult.Value;
        // Use Works DbSet and OfType for type-specific logic
        var work = await context.Works
            .Include(w => w.Owner)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (work is null)
            return Errors.WorkNotFound;
        if (work.OwnerId != author.Id)
            return Errors.OnlyOwnerCanPublish;
        if (work.IsPublished && !body.PublishAll)
            return Errors.AlreadyPublished;

        bool hasContent = false;
        bool hasChildren = false;
        int childCount = 0;

        if (work is Story story)
        {
            await context.Entry(story).Collection(s => s.Chapters).LoadAsync(cancellationToken);
            await context.Entry(story).Collection(s => s.Books).LoadAsync(cancellationToken);
            hasContent = story.HasContent;
            hasChildren = story.HasChapters || story.HasBooks;
            childCount = story.Chapters.Count + story.Books.Count;
        }
        else if (work is Book book)
        {
            await context.Entry(book).Collection(b => b.Chapters).LoadAsync(cancellationToken);
            hasContent = book.HasContent;
            hasChildren = book.Chapters.Count > 0;
            childCount = book.Chapters.Count;
        }
        else if (work is Chapter chapter)
        {
            hasContent = chapter.WorkBodyId != default;
            hasChildren = false;
            childCount = 0;
        }
        else if (work is Anthology anthology)
        {
            await context.Entry(anthology).Collection(a => a.Stories).LoadAsync(cancellationToken);
            hasContent = false;
            hasChildren = anthology.Stories.Count > 0;
            childCount = anthology.Stories.Count;
        }
        if (!hasContent && !hasChildren)
            return Errors.NoContentToPublish;
        try
        {
            // Publish this work
            work.PublishedAt = dateTimeProvider.GetUtcNow().UtcDateTime;
            // Optionally publish all children
            if (body.PublishAll)
            {
                switch (work)
                {
                    case Story s:
                        foreach (var b in s.Books)
                        {
                            if (!b.IsPublished)
                                b.PublishedAt = dateTimeProvider.GetUtcNow().UtcDateTime;
                            foreach (var c in b.Chapters)
                                if (!c.IsPublished)
                                    c.PublishedAt = dateTimeProvider.GetUtcNow().UtcDateTime;
                        }
                        foreach (var c in s.Chapters)
                            if (!c.IsPublished)
                                c.PublishedAt = dateTimeProvider.GetUtcNow().UtcDateTime;
                        break;
                    case Book b:
                        foreach (var c in b.Chapters)
                            if (!c.IsPublished)
                                c.PublishedAt = dateTimeProvider.GetUtcNow().UtcDateTime;
                        break;
                    case Anthology a:
                        foreach (var s2 in a.Stories)
                            if (!s2.IsPublished)
                                s2.PublishedAt = dateTimeProvider.GetUtcNow().UtcDateTime;
                        break;
                }
            }
            await context.SaveChangesAsync(cancellationToken);
            return new PublishWorkResponse(
                work.Id,
                work.Title,
                work.GetType().Name,
                work.PublishedAt.Value,
                work.UpdatedAt,
                hasContent,
                hasChildren,
                childCount);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Errors.ConcurrencyConflict;
        }
        catch (DbUpdateException)
        {
            return Errors.DatabaseError;
        }
    }
    public static string EndpointName => nameof(PublishWork);
    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;
        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("works/{id:ulid}/publish", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                [FromBody] PublishWorkBody body,
                PublishWork useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, body, claimsPrincipal, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Publish Work (Story, Book, Chapter, Anthology)")
            .WithDescription("Makes any work publicly visible by setting its publication timestamp. " +
                "If publishAll is true, recursively publishes all child works. Only owners can publish their works. " +
                "Requires authentication and ownership.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses(conflict: false)
            .Produces<Linked<PublishWorkResponse>>();
        }
    }
}
