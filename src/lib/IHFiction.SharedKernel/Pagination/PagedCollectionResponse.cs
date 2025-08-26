using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedKernel.Pagination;

public record PagedCollectionResponse(
    IQueryable<object> Data,
    int TotalCount,
    int TotalPages,
    int CurrentPage,
    int PageSize,
    bool HasNextPage,
    bool HasPreviousPage
) : CollectionResponse<object>(Data)
{
    public static new PagedCollectionResponse<TData> Empty<TData>() => new(Enumerable.Empty<TData>().AsQueryable(), 0, 1, 0, 0, false, false);
};

/// <summary>
/// Standardized response model for paginated collections with full pagination metadata.
/// Use this for endpoints that return paginated collections and need to provide pagination information to clients.
/// This type can be inherited from to create domain-specific response types while maintaining consistency.
/// </summary>
/// <typeparam name="TData">The type of items in the collection</typeparam>
/// <param name="Data">The collection of items for the current page</param>
/// <param name="TotalCount">Total number of items across all pages</param>
/// <param name="TotalPages">Total number of pages available</param>
/// <param name="CurrentPage">Current page number (1-based indexing)</param>
/// <param name="PageSize">Number of items per page</param>
/// <param name="HasNextPage">Whether there are more pages after the current page</param>
/// <param name="HasPreviousPage">Whether there are pages before the current page</param>
public record PagedCollectionResponse<TData>(
    IQueryable<TData> Data,
    int TotalCount,
    int TotalPages,
    int CurrentPage,
    int PageSize,
    bool HasNextPage,
    bool HasPreviousPage
) : CollectionResponse<TData>(Data), ICollectionPagedJson<TData>;


