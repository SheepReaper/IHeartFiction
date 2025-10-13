using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Pagination;

namespace IHFiction.FictionApi.Stories;

internal sealed class ListPublishedStoryChapters(
    FictionDbContext fictionDb,
    IPaginationService paginator) : IUseCase, INameEndpoint<ListPublishedStoryChapters>
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly DomainError DatabaseError = CommonErrors.Database.ConnectionFailed;

        // Business logic errors specific to listing story chapters
        public static readonly DomainError StoryNotFound = new("ListStoryChapters.StoryNotFound", "Story not found.");
        public static readonly DomainError StoryNotPublished = new("ListStoryChapters.StoryNotPublished", "Story is not published.");
        public static readonly DomainError InvalidSortBy = new("ListStoryChapters.InvalidSortBy", "Sort by must be one of: title, createdAt, updatedAt, publishedAt.");
        public static readonly DomainError InvalidSortOrder = new("ListStoryChapters.InvalidSortOrder", "Sort order must be either 'asc' or 'desc'.");
    }

    internal sealed record ListPublishedStoryChaptersQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<ListPublishedStoryChaptersItem>]
        string Fields = ""
    ) : IDataShapingSupport;

    /// <summary>
    /// Represents a single chapter item in the story chapters list.
    /// </summary>
    /// <param name="ChapterId">Unique identifier for the chapter</param>
    /// <param name="Title">Title of the chapter</param>
    /// <param name="Order">Order of the chapter within the story</param>
    /// <param name="PublishedAt">When the chapter was published (null if unpublished)</param>
    /// <param name="CreatedAt">When the chapter was created</param>
    /// <param name="UpdatedAt">When the chapter was last updated</param>
    /// <param name="HasContent">Whether the chapter has content written</param>
    /// <param name="ContentLength">Length of the chapter content in characters</param>
    internal sealed record ListPublishedStoryChaptersItem(
        Ulid ChapterId,
        string Title,
        int Order,
        DateTime? PublishedAt,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        bool HasContent,
        int ContentLength);

    public async Task<Result<PagedCollection<ListPublishedStoryChaptersItem>>> HandleAsync(
        Ulid id,
        CancellationToken cancellationToken = default)
    {
        var story = await fictionDb.Stories.FindAsync([id], cancellationToken);

        if (story is null)
            return Errors.StoryNotFound;

        // Only allow access to published stories
        if (!story.IsPublished)
            return Errors.StoryNotPublished;

        // Build chapter query - only show published chapters for public access
        var chaptersQuery = fictionDb.Chapters
            .Where(c => c.StoryId == id)
            .Where(c => c.PublishedAt != null)
            .OrderBy(c => c.PublishedAt);

        // Map to response items
        var chapterItems = chaptersQuery.Select(c => new ListPublishedStoryChaptersItem(
            c.Id,
            c.Title,
            c.Order,
            c.PublishedAt,
            c.CreatedAt,
            c.UpdatedAt,
            c.WorkBodyId != default,
            0 // TODO: ContentLength - would need separate query to WorkBody collection
        ));

        return await paginator.ExecutePagedQueryAsync(chapterItems, cancellationToken: cancellationToken);
    }
    public static string EndpointName => nameof(ListPublishedStoryChapters);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("stories/{id:ulid}/chapters", async (
                [FromRoute] Ulid id,
                [AsParameters] ListPublishedStoryChaptersQuery query,
                ListPublishedStoryChapters useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("List Published Story Chapters")
            .WithDescription("Retrieves a list of published chapters for a specific published story. " +
                "This is a public endpoint that only shows published chapters from published stories. " +
                "Chapters can be sorted by various fields. No authentication required.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(unauthorized: false, conflict: false)
            .Produces<LinkedPagedCollection<ListPublishedStoryChaptersItem>>();
        }
    }
}
