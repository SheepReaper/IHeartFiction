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

public interface ICollectionPagedLinked<TData> : ICollectionPaged<Linked<TData>>, ILinks;

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


