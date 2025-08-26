using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.Pagination;

namespace IHFiction.FictionApi.Common;

/// <summary>
/// Centralized service for handling pagination logic across list endpoints.
/// Uses dependency injection with configuration options to eliminate parameter normalization.
/// Focuses solely on pagination concerns with sorting handled separately by SortingService.
/// </summary>
/// <remarks>
/// Initializes a new instance of the PaginationService with configuration options.
/// </remarks>
/// <param name="options">Pagination configuration options</param>
internal sealed class PaginationService(IOptions<PaginationOptions> options) : IPaginationService
{
    private readonly PaginationOptions _options = options.Value;


    /// <summary>
    /// Creates a pagination request with defaults applied from configuration.
    /// Validates page and page size parameters against configured limits.
    /// </summary>
    /// <param name="page">Requested page number (null uses default)</param>
    /// <param name="pageSize">Requested page size (null uses default)</param>
    /// <param name="configureOptions">Optional configuration override</param>
    /// <returns>Validated pagination request</returns>
    public PaginationParams CreatePaginationRequest(
        int? page = null,
        int? pageSize = null,
        Action<PaginationOptions>? configureOptions = null)
    {
        // Apply configuration overrides if provided
        var pOptions = new PaginationOptions
        {
            DefaultPage = _options.DefaultPage,
            DefaultPageSize = _options.DefaultPageSize,
            MaxPageSize = _options.MaxPageSize
        };

        configureOptions?.Invoke(pOptions);


        page = Math.Clamp(page ?? pOptions.DefaultPage, 1, int.MaxValue);
        pageSize = Math.Clamp(pageSize ?? pOptions.DefaultPageSize, 1, pOptions.MaxPageSize);

        // Validate against maximum page size
        return pageSize > pOptions.MaxPageSize
            ? throw new ArgumentException($"Page size {pageSize} exceeds maximum allowed size of {pOptions.MaxPageSize}.")
            : new PaginationParams(page, pageSize);
    }

    /// <summary>
    /// Executes a paginated query and returns results with pagination metadata.
    /// Assumes sorting has already been applied to the query by SortingService.
    /// </summary>
    /// <typeparam name="TSource">The type of items in the query</typeparam>
    /// <param name="query">The queryable to paginate (should already be sorted)</param>
    /// <param name="pagination">Pagination parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated result with metadata</returns>
    public async Task<PagedCollection<TSource>> ExecutePagedQueryAsync<TSource>(
        IQueryable<TSource> query,
        IPaginationSupport? pagination = null,
        CancellationToken cancellationToken = default)
    {
        pagination ??= new PaginationParams();

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Clamp requested dimensions
        var pageSize = Math.Clamp(pagination.PageSize ?? _options.DefaultPageSize, 1, _options.MaxPageSize);

        // calculate the last page number based of the total count and page size
        var lastPageIndex = totalCount / pageSize;

        var page = Math.Clamp(pagination.Page ?? _options.DefaultPage, 1, lastPageIndex + 1);

        return query.ToPagedResult(totalCount, page, pageSize);
    }
}

/// <summary>
/// Represents pagination parameters with defaults applied via configuration.
/// All properties are non-nullable since defaults are handled by PaginationOptions.
/// </summary>
internal record PaginationParams(
    int? Page = null,
    int? PageSize = null) : IPaginationSupport;
