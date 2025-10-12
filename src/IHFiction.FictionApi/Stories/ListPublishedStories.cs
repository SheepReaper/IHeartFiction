using System.ComponentModel.DataAnnotations;
using System.Net.Mime;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Pagination;
using IHFiction.SharedKernel.Searching;
using IHFiction.SharedKernel.Sorting;

namespace IHFiction.FictionApi.Stories;

internal sealed class ListPublishedStories(
    FictionDbContext context,
    IPaginationService paginator) : IUseCase, INameEndpoint<ListPublishedStories>
{
    internal sealed record ListPublishedStoriesQuery(
        [property: Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        int? Page = null,

        [property: Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
        int? PageSize = null,

        [property: FromQuery(Name = "Q")]
        [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Search term must be between 2 and 100 characters.")]
        string? Search = null,

        [property: StringLength(50, ErrorMessage = "Sort field must be 50 characters or less.")]
        string Sort = "publishedAt",

        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<ListPublishedStoriesItem>]
        string? Fields = null
    ) : IPaginationSupport, ISearchSupport, ISortingSupport, IDataShapingSupport;

    private static readonly SortMapping[] SortMappings = [
        new(nameof(Story.PublishedAt)),
        new(nameof(Story.Title)),
        new(nameof(Story.UpdatedAt))];

    /// <summary>
    /// Request model for listing published stories.
    /// </summary>
    /// <param name="AuthorId">Limit results to stories by a specific author.</param>
    internal sealed record ListPublishedStoriesBody(
        Ulid? AuthorId = null
    );

    /// <summary>
    /// Represents a single published story item in the stories list response.
    /// </summary>
    /// <param name="StoryId">Unique identifier for the story</param>
    /// <param name="Title">Title of the story</param>
    /// <param name="Description">Description of the story</param>
    /// <param name="PublishedAt">When the story was published</param>
    /// <param name="UpdatedAt">When the story was last updated</param>
    /// <param name="HasContent">Whether the story has direct content</param>
    /// <param name="HasChapters">Whether the story has chapters</param>
    /// <param name="HasBooks">Whether the story has books</param>
    /// <param name="ChapterCount">Number of chapters in the story</param>
    /// <param name="AuthorId">Unique identifier for the story author</param>
    /// <param name="AuthorName">Name of the story author</param>
    internal sealed record ListPublishedStoriesItem(
        Ulid StoryId,
        string Title,
        string Description,
        DateTime PublishedAt,
        DateTime UpdatedAt,
        bool HasContent,
        bool HasChapters,
        bool HasBooks,
        int ChapterCount,
        Ulid AuthorId,
        string AuthorName);

    public async Task<Result<PagedCollection<ListPublishedStoriesItem>>> HandleAsync(
        ListPublishedStoriesQuery query,
        ListPublishedStoriesBody body,
        CancellationToken cancellationToken = default)
    {
        // Build the base query for published stories
        var stories = context.Stories
            .Include(s => s.Owner)
            .Include(s => s.Chapters)
            .Include(s => s.Books)
            .Include(s => s.Authors)
            .Where(s => s.PublishedAt != null)
            .Where(s => body.AuthorId == null || s.Authors.Any(a => a.Id == body.AuthorId))
            .AsNoTracking();

        // Apply search filter if provided
        stories = stories.SearchIContains(query, s => s.Title, s => s.Description, s => s.Owner.Name);

        // Apply sorting
        stories = stories.ApplySort(query, SortMappings);

        // Apply projection
        var proj = stories.Select(s => new ListPublishedStoriesItem(
            s.Id,
            s.Title,
            s.Description,
            s.PublishedAt!.Value, // Safe because we filtered for non-null
            s.UpdatedAt,
            s.HasContent,
            s.HasChapters,
            s.HasBooks,
            s.Chapters.Count,
            s.OwnerId,
            s.Owner.Name));

        // Execute paginated query using the centralized service
        return await paginator.ExecutePagedQueryAsync(proj, query, cancellationToken);
    }
    public static string EndpointName => nameof(ListPublishedStories);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("stories/published", async (
                [AsParameters] ListPublishedStoriesQuery query,
                [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ListPublishedStoriesBody? body,
                ListPublishedStories useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(query, body ?? new(), cancellationToken);

                return result.ToOkResult(query);
            }
                )
                .WithSummary("List Published Stories")
                .WithDescription("Retrieves a paginated list of all publicly published stories. " +
                "Supports searching by title, description, or author name. " +
                "Stories can be sorted by publication date, title, or last update date. " +
                "This is a public endpoint that does not require authentication and only " +
                "returns stories that have been explicitly published by their authors.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous() // Public endpoint - no authentication required
            .WithStandardResponses(notFound: false, conflict: false, unauthorized: false, forbidden: false)
            .Produces<LinkedPagedCollection<ListPublishedStoriesItem>>()
            .Accepts<ListPublishedStoriesBody>(true, MediaTypeNames.Application.Json);
        }
    }
}
