using System.ComponentModel.DataAnnotations;
using System.Net.Mime;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Pagination;
using IHFiction.SharedKernel.Searching;
using IHFiction.SharedKernel.Sorting;

namespace IHFiction.FictionApi.Tags;

internal sealed class ListTags(
    FictionDbContext context,
    IPaginationService paginator) : IUseCase, INameEndpoint<ListTags>
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly DomainError DatabaseError = CommonErrors.Database.ConnectionFailed;

        // Business logic errors specific to listing tags
        public static readonly DomainError InvalidPageSize = new("ListTags.InvalidPageSize", "Page size must be between 1 and 200.");
        public static readonly DomainError InvalidPage = new("ListTags.InvalidPage", "Page must be greater than 0.");
        public static readonly DomainError InvalidSortBy = new("ListTags.InvalidSortBy", "Sort by must be one of: category, value, usage, createdAt.");
        public static readonly DomainError InvalidSortOrder = new("ListTags.InvalidSortOrder", "Sort order must be either 'asc' or 'desc'.");
    }

    /// <summary>
    /// Request model for listing tags with pagination, search, and filtering capabilities.
    /// </summary>
    /// <param name="Category">Optional filter to show tags from a specific category only.</param>
    internal sealed record ListTagsBody(
        [property: StringLength(50, ErrorMessage = "Category must be 50 characters or less.")]
        string? Category = null
    );

    internal sealed record ListTagsQuery(
        [property: Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        int? Page = 1,

        [property: Range(1, 200, ErrorMessage = "Page size must be between 1 and 200.")]
        int? PageSize = 50,

        [property: FromQuery(Name = "Q")]
        [property: StringLength(100, ErrorMessage = "Search term must be 100 characters or less.")]
        string? Search = null,


        string Sort = "category",

        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<ListTagsItem>]
        string? Fields = null
    ) : IPaginationSupport, ISearchSupport, ISortingSupport, IDataShapingSupport
    {
        /// <summary>
        /// Optional filter to show tags from a specific category only.
        /// </summary>
        /// <example>lol,me,you,them</example>
        [StringLength(50, ErrorMessage = "Sort field must be 50 characters or less.")]
        public string? Sort { get; set; } = Sort;
    };

    private static readonly SortMapping[] SortMappings = [
        new(nameof(Tag.Category)),
        new(nameof(Tag.Value)),
        new(nameof(Tag.Works.Count)),
        new(nameof(Tag.CreatedAt))];

    /// <summary>
    /// Represents a single tag item in the tags list response.
    /// </summary>
    /// <param name="TagId">Unique identifier for the tag</param>
    /// <param name="Category">The category this tag belongs to (e.g., "genre", "theme")</param>
    /// <param name="Subcategory">Optional subcategory for more specific classification</param>
    /// <param name="Value">The actual tag value</param>
    /// <param name="CreatedAt">When the tag was first created</param>
    /// <param name="StoryCount">Number of stories that use this tag</param>
    /// <param name="DisplayFormat">Formatted display string for the tag</param>
    internal sealed record ListTagsItem(
        Ulid TagId,
        string Category,
        string? Subcategory,
        string Value,
        DateTime CreatedAt,
        int StoryCount,
        string DisplayFormat);

    public async Task<Result<PagedCollection<ListTagsItem>>> HandleAsync(
        ListTagsQuery query,
        ListTagsBody body,
        CancellationToken cancellationToken = default)
    {
        // Build the base query for all tags (includes both canonical and synonym tags)
        // In a full implementation, we'd filter for canonical tags only
        var tags = context.Tags
            .AsNoTracking();

        // Apply category filter if provided
        // if (!string.IsNullOrWhiteSpace(request.Category))
        // {
        //     var categoryFilter = request.Category.Trim();
        //     query = query.Where(t => t.Category.Contains(categoryFilter, StringComparison.OrdinalIgnoreCase));
        // }

        tags = tags.SearchIContains(body.Category, t => t.Category);

        // Apply search filter if provided
        tags = tags.SearchIContains(query.Search, t => t.Category, t => t.Subcategory, t => t.Value);

        // Apply sorting
        tags = tags.ApplySort(query, SortMappings);

        // Apply pagination and select results with usage statistics
        var proj = tags.Select(t => new ListTagsItem(
            t.Id,
            t.Category,
            t.Subcategory,
            t.Value,
            t.CreatedAt,
            t.Works.Count(w => w.PublishedAt != null), // Only count published stories
            t.ToString()));

        return await paginator.ExecutePagedQueryAsync(proj, query, cancellationToken);
    }

    public static string EndpointName => nameof(ListTags);


    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("tags", async (
                [AsParameters] ListTagsQuery query,
                [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ListTagsBody? body,
                ListTags useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(query, body ?? new(), cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("List Tags")
            .WithDescription("Retrieves a paginated list of all available tags used across stories. " +
                "Supports filtering by category and searching by tag value. " +
                "Tags can be sorted by category, value, usage count, or creation date. " +
                "This is a public endpoint that does not require authentication.")
            .WithTags(ApiTags.Tags.Discovery)
            .AllowAnonymous() // Public endpoint - no authentication required
            .WithStandardResponses(conflict: false, notFound: false, unauthorized: false, forbidden: false)
            .Produces<LinkedPagedCollection<ListTagsItem>>()
            .Accepts<ListTagsBody>(true, MediaTypeNames.Application.Json);
        }
    }
}
