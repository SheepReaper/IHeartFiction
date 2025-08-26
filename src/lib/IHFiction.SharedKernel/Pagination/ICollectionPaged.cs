using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedKernel.Pagination;

public interface ICollectionPaged<out TData> : ICollectionResponse<TData>
{
    int TotalCount { get; }
    int PageSize { get; }
    int CurrentPage { get; }
    int TotalPages { get; }
}

/// <summary>
/// A paginated collection of items.
/// </summary>
/// <typeparam name="TData">The type of items in the collection</typeparam>
/// <param name="Data">The items in the collection</param>
/// <param name="TotalCount">The total number of items in the collection</param>
/// <param name="CurrentPage">The current page number</param>
/// <param name="PageSize">The number of items per page</param>
public abstract record CollectionPaged<TData>(IQueryable<TData> Data, int TotalCount, int CurrentPage, int PageSize) : CollectionResponse<TData>(Data), ICollectionPaged<TData>
{
    /// <summary>
    /// Total number of pages based on total count and page size.
    /// </summary>
    public virtual int TotalPages => (int)MathF.Ceiling((float)TotalCount / PageSize);
}


