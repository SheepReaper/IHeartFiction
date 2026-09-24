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
using IHFiction.SharedKernel.Linking;
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
        int Page = 1,

        [property: Range(1, 200, ErrorMessage = "Page size must be between 1 and 200.")]
        int PageSize = 50,

        [property: FromQuery(Name = "Q")]
        [property: StringLength(100, ErrorMessage = "Search term must be 100 characters or less.")]
        string Search = "",

        [property: StringLength(50, ErrorMessage = "Sort field must be 50 characters or less.")]
        string Sort = "category",

        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<ListTagsItem>]
        string Fields = ""
    ) : IPaginationSupport, ISearchSupport, ISortingSupport, IDataShapingSupport;

    private static readonly SortMapping[] SortMappings = [
        new(nameof(ListTagsItem.Category)),
        new(nameof(ListTagsItem.Value)),
        new(nameof(ListTagsItem.StoryCount)),
        new(nameof(ListTagsItem.CreatedAt))];

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
        var tags = await context.Tags
            .AsNoTracking()
            .OfType<CanonicalTag>()
            .Include(tag => tag.Synonyms)
            .ToListAsync(cancellationToken);

        var categoryFilter = InputSanitizationService.SanitizeTag(body.Category);
        if (!string.IsNullOrWhiteSpace(categoryFilter))
        {
            tags = [.. tags.Where(tag => tag.Category.Equals(categoryFilter, StringComparison.OrdinalIgnoreCase))];
        }

        var searchFilter = InputSanitizationService.SanitizeTag(query.Search);
        if (!string.IsNullOrWhiteSpace(searchFilter))
        {
            var parsedSearch = TryParseTagSearch(searchFilter);

            tags = [.. tags
                .Where(tag =>
                    MatchesTagSearch(tag, parsedSearch, searchFilter)
                    || tag.Synonyms.Any(synonym => MatchesTagSearch(synonym, parsedSearch, searchFilter)))];
        }

        var proj = tags
            .Select(t => new ListTagsItem(
                t.Id,
                t.Category,
                t.Subcategory,
                t.Value,
                t.CreatedAt,
                t.Works.Count(w => w.PublishedAt != null),
                t.ToString()))
            .AsQueryable();

        proj = proj.ApplySort(query, SortMappings);

        return await paginator.ExecutePagedQueryAsync(proj, query, cancellationToken);
    }

    private static (string Category, string? Subcategory, string Value)? TryParseTagSearch(string search)
    {
        var parts = search.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 3)
        {
            return null;
        }

        return parts.Length == 2
            ? (parts[0], null, parts[1])
            : (parts[0], parts[1], parts[2]);
    }

    private static bool MatchesTagSearch(Tag tag, (string Category, string? Subcategory, string Value)? parsedSearch, string rawSearch)
    {
        if (parsedSearch is { } parsed)
        {
            return TagCanonicalizationService.Matches(
                parsed.Category,
                parsed.Subcategory,
                parsed.Value,
                tag.Category,
                tag.Subcategory,
                tag.Value);
        }

        return tag.Value.StartsWith(rawSearch, StringComparison.OrdinalIgnoreCase) ||
               tag.Category.StartsWith(rawSearch, StringComparison.OrdinalIgnoreCase) ||
               (tag.Subcategory is not null && tag.Subcategory.StartsWith(rawSearch, StringComparison.OrdinalIgnoreCase));
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
                LinkService linker,
                CancellationToken cancellationToken) =>
            {
                var result = (await useCase.HandleAsync(query, body ?? new(), cancellationToken))
                    .WithLinks(
                        linker,
                        Name,
                        tag => new(tag, new List<LinkItem>()),
                        query);

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
