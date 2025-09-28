using IHFiction.SharedKernel.Linking;

namespace IHFiction.SharedKernel.Pagination;

public interface ICollectionPagedJson<out TData> : ICollectionPaged<TData>
{
    bool HasNextPage { get; }
    bool HasPreviousPage { get; }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Openapi naming convention.")]
public record PagedCollection<TData>(
    IQueryable<TData> Data,
    int TotalCount,
    int CurrentPage,
    int PageSize) : CollectionPaged<TData>(
        Data,
        TotalCount,
        CurrentPage,
        PageSize), ICollectionPagedJson<TData>
{
    public virtual bool HasNextPage => CurrentPage < TotalPages;
    public virtual bool HasPreviousPage => CurrentPage > 1;
}

/// <summary>
/// A paginated collection response of <typeparamref name="TData"/> items with hypermedia links.
/// Both the items and the collection itself include associated links.
/// </summary> 
/// <typeparam name="TData">The type of items in the collection</typeparam>
public interface ICollectionPagedLinked<TData> : ICollectionPaged<Linked<TData>>, ILinks;

/// <summary>
/// A standard response model for a paginated collection of <typeparamref name="TData"/> items,
/// where each item is wrapped in a <see cref="Linked{T}"/> to include hypermedia links.
/// The collection itself also includes a set of hypermedia links.
/// </summary>
/// <typeparam name="TData">The type of items in the collection</typeparam>
/// <param name="Data">The collection of items for the current page</param>
/// <param name="TotalCount">Total number of items across all pages</param>
/// <param name="CurrentPage">Current page number (1-based indexing)</param>
/// <param name="PageSize">Number of items per page</param>
/// <param name="Links">Collection-level hypermedia links</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Openapi naming convention.")]
public record LinkedPagedCollection<TData>(
    IQueryable<Linked<TData>> Data,
    int TotalCount,
    int CurrentPage,
    int PageSize,
    IEnumerable<LinkItem> Links
) : CollectionPaged<Linked<TData>>(
    Data,
    TotalCount,
    CurrentPage,
    PageSize
), ICollectionPagedLinked<TData>;


