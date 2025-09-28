using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Pagination;
using IHFiction.SharedKernel.Searching;
using IHFiction.SharedKernel.Sorting;
using IHFiction.SharedKernel.Validation;

namespace IHFiction.FictionApi.Stories;

internal sealed class GetCurrentAuthorStories(
    FictionDbContext context,
    AuthorizationService authorizationService,
    IPaginationService paginator) : IUseCase, INameEndpoint<GetCurrentAuthorStories>
{
    /// <summary>
    /// Request model for getting the current author's stories with filtering and pagination.
    /// </summary>
    /// <param name="Tags">Optional comma-separated list of tags to filter stories by.</param>
    /// <param name="IsPublished">Filter by publication status. True for published stories, false for unpublished, null for all.</param>
    /// <param name="IsOwned">Filter by ownership. True for owned stories, false for collaborated stories, null for all.</param>
    internal sealed record GetCurrentAuthorStoriesBody(
        [property: StringLength(200, ErrorMessage = "Tags filter must be 200 characters or less.")]
        [property: NoHarmfulContent]
        string? Tags = null,
        bool? IsPublished = null,
        bool? IsOwned = null
    );

    internal sealed record Query(
        [property: Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
        int? PageSize = null,
        [property: Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0.")]
        int? Page = null,
        [property: StringLength(100, ErrorMessage = "Search query must be 100 characters or less.")]
        [property: NoHarmfulContent]
        [property: FromQuery(Name = "Q")]
        string? Search = null,
        string? Sort = null,
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<AuthorStoryItem>]
        string? Fields = null
    ) : IPaginationSupport, ISearchSupport, ISortingSupport, IDataShapingSupport;

    private static readonly SortMapping[] SortMappings = [
        new(nameof(Story.Title)),
        new(nameof(Story.CreatedAt)),
        new(nameof(Story.UpdatedAt))];

    /// <summary>
    /// Represents a single story item in the current author's stories list.
    /// </summary>
    /// <param name="Id">Unique identifier for the story</param>
    /// <param name="Title">Title of the story</param>
    /// <param name="Description">Description of the story</param>
    /// <param name="PublishedAt">When the story was published (null if unpublished)</param>
    /// <param name="IsPublished">Whether the story is currently published</param>
    /// <param name="UpdatedAt">When the story was last updated</param>
    /// <param name="CreatedAt">When the story was created</param>
    /// <param name="IsOwned">Whether the current author owns this story</param>
    /// <param name="CollaboratorNames">Names of collaborators on the story</param>
    /// <param name="Tags">Tags associated with the story</param>
    /// <param name="HasContent">Whether the story has direct content</param>
    /// <param name="HasChapters">Whether the story has chapters</param>
    /// <param name="HasBooks">Whether the story has books</param>
    /// <param name="IsValid">Whether the story is in a valid state for operations</param>
    internal sealed record AuthorStoryItem(
        Ulid Id,
        string Title,
        string Description,
        DateTime? PublishedAt,
        bool IsPublished,
        DateTime UpdatedAt,
        DateTime CreatedAt,
        bool IsOwned,
        IEnumerable<string> CollaboratorNames,
        IEnumerable<string> Tags,
        bool HasContent,
        bool HasChapters,
        bool HasBooks,
        bool IsValid);

    public async Task<Result<PagedCollection<AuthorStoryItem>>> HandleAsync(
        Query query,
        GetCurrentAuthorStoriesBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        // Create pagination request using the service
        var pagination = paginator.CreatePaginationRequest(query.Page, query.PageSize);

        // Get the current author using the centralized authorization service
        var authorResult = await authorizationService.GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);
        if (authorResult.IsFailure) return authorResult.DomainError;

        var author = authorResult.Value;

        string[] requestTags = body.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        IQueryable<Tag>? tags = requestTags.Length > 0 ? context.Tags.Where(t => requestTags.Any(rt => t.Value == rt)) : null;

        var stories = context.Stories
            .Include(s => s.Owner)
            .Include(s => s.Authors)
            .Include(s => s.Tags)
            .Include(s => s.Chapters)
            .Include(s => s.Books)
            .AsNoTracking()
            .Where(s => s.Authors.Any(a => a.Id == author.Id) || s.OwnerId == author.Id)
            .Where(s => tags == null || tags.All(t => s.Tags.Contains(t)))
            .Where(s => body.IsPublished == null || s.IsPublished == body.IsPublished);

        // Apply filters
        // stories = ApplyFilters(stories, query, author.Id);
        stories = stories.SearchIContains(query, s => s.Title, s => s.Description);

        // Apply sorting
        stories = stories.ApplySort(query, SortMappings);

        // Execute paginated query using the centralized service
        var page = await paginator.ExecutePagedQueryAsync(
            stories.Select(s => new AuthorStoryItem(
                s.Id,
                s.Title,
                s.Description,
                s.PublishedAt,
                s.IsPublished,
                s.UpdatedAt,
                s.CreatedAt,
                s.OwnerId == author.Id,
                s.Authors.Where(a => a.Id != author.Id).Select(a => a.Name),
                s.Tags.Select(t => t.ToString()),
                s.HasContent,
                s.HasChapters,
                s.HasBooks,
                s.IsValid)),
            pagination,
            cancellationToken);

        return page;
    }
    public static string EndpointName => nameof(GetCurrentAuthorStories);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;


        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("me/stories", async (
                [AsParameters] Query query,
                [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] GetCurrentAuthorStoriesBody? body,
                GetCurrentAuthorStories useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(query, body ?? new(), claimsPrincipal, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Get Current Author Stories")
            .WithDescription("Retrieves a paginated list of stories associated with the current authenticated author. " +
                "Includes both owned stories and stories where the author is a collaborator. " +
                "Supports filtering by publication status, ownership, tags, and search terms. " +
                "Stories can be sorted by various fields. Requires authentication and author registration.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization("author")
            .WithStandardResponses(conflict: false, notFound: false)
            .Accepts<GetCurrentAuthorStoriesBody>(true, MediaTypeNames.Application.Json)
            .Produces<LinkedPagedCollection<AuthorStoryItem>>();
        }
    }
}
