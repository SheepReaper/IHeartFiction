using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Pagination;
using IHFiction.SharedKernel.Searching;
using IHFiction.SharedKernel.Sorting;

namespace IHFiction.FictionApi.Authors;

internal sealed class ListAuthors(
    FictionDbContext context,
    IPaginationService paginator) : IUseCase, INameEndpoint<ListAuthors>
{
    /// <summary>
    /// Request model for listing authors with pagination, search, and sorting capabilities.
    /// </summary>
    /// <param name="Page">Optional page number for pagination. Defaults to 1.</param>
    /// <param name="PageSize">Optional page size for pagination. Defaults to 50.</param>
    /// <param name="Search">Optional search term to filter authors by name or biography.</param>
    /// <param name="Sort">Optional field to sort results by. Defaults to "name".</param>
    /// <param name="Fields">Optional comma-separated list of fields to include in the response.</param>
    internal sealed record ListAuthorsQuery(
        [property: Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        int Page = 1,

        [property: Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
        int PageSize = 30,

        [property: FromQuery(Name = "Q")]
        [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Search term must be between 2 and 100 characters.")]
        string Search = "",

        [property: StringLength(50, ErrorMessage = "Sort field must be 50 characters or less.")]
        string Sort = "name",

        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<ListAuthorsItem>]
        string Fields = ""
    ) : IPaginationSupport, ISearchSupport, ISortingSupport, IDataShapingSupport;

    private static readonly SortMapping[] SortMappings = [
        new(nameof(Author.Name)),
        new(nameof(Author.CreatedAt)),
        new(nameof(Author.UpdatedAt))];

    /// <summary>
    /// Represents a single author item in the authors list response.
    /// </summary>
    /// <param name="Id">Unique identifier for the author</param>
    /// <param name="Name">Display name of the author</param>
    /// <param name="Bio">Author's biography or description</param>
    /// <param name="CreatedAt">When the author profile was created</param>
    /// <param name="UpdatedAt">When the author profile was last updated</param>
    /// <param name="TotalStories">Total number of stories by this author</param>
    /// <param name="PublishedStories">Number of published stories by this author</param>
    internal sealed record ListAuthorsItem(
        Ulid Id,
        string Name,
        string Bio,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        int TotalStories,
        int PublishedStories);

    public async Task<Result<PagedCollection<ListAuthorsItem>>> HandleAsync(
        ListAuthorsQuery query,
        CancellationToken cancellationToken = default)
    {
        // Build the base query for authors with at least one published story
        var authors = context.Authors
            .Include(a => a.Profile)
            .Include(a => a.Works)
            .Where(a => a.Works.Any(w => w is Story && w.PublishedAt != null))
            .AsNoTracking();

        // Apply search using the centralized service with multiple fields
        authors = authors.SearchIContains(query, a => a.Name, a => a.Profile.Bio);

        // Apply sorting using the centralized SortingService with type-safe mappings
        authors = authors.ApplySort(query, SortMappings);

        // Apply projection
        var proj = authors
            .Select(a => new ListAuthorsItem(
                a.Id,
                a.Name,
                a.Profile.Bio ?? "",
                a.CreatedAt,
                a.UpdatedAt,
                a.Works.Count(w => w is Story),
                a.Works.Count(w => w is Story && w.PublishedAt != null)));

        // Execute paginated query using the centralized service
        return await paginator.ExecutePagedQueryAsync(proj, query, cancellationToken);
    }
    public static string EndpointName => nameof(ListAuthors);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("authors", async (
                [AsParameters] ListAuthorsQuery query,
                ListAuthors useCase,
                LinkService linker,
                CancellationToken cancellationToken) =>
            {
                var result = (await useCase
                    .HandleAsync(query, cancellationToken))
                    .WithLinks(
                        linker,
                        Name,
                        a => new(a, new List<LinkItem>() {
                            linker.Create<GetAuthor>("self", HttpMethods.Get, new{ a.Id })}),
                        query);

                return result.ToOkResult(query);
            })
            .WithSummary("List Authors")
            .WithDescription("Retrieves a paginated list of all authors who have published at least one story. " +
            "Supports searching by author name or biography content. " +
            "Authors can be sorted by name, creation date, or last update date. " +
            "This is a public endpoint that does not require authentication.")
            .WithTags(ApiTags.Authors.Discovery)
            .AllowAnonymous() // Public endpoint - no authentication required
            .WithStandardResponses(notFound: false, conflict: false, unauthorized: false, forbidden: false)
            .Produces<LinkedPagedCollection<ListAuthorsItem>>();
        }
    }
}
