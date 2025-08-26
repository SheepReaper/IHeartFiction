using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedKernel.Linking;

/// <summary>
/// A paginated collection of items that have hypermedia links.
/// The collection itself also has hypermedia links.
/// </summary>
/// <typeparam name="TData">The type of items in the collection</typeparam>
/// <param name="Data">The items in the collection</param>
/// <param name="TotalCount">The total number of items in the collection</param>
/// <param name="TotalPages">The total number of pages in the collection</param>
/// <param name="CurrentPage">The current page number</param>
/// <param name="PageSize">The number of items per page</param>
/// <param name="Links">The collection's hypermedia links</param>
public record LinkedPagedCollectionResponse<TData>(
    IQueryable<Linked<TData>> Data,
    int TotalCount,
    int TotalPages,
    int CurrentPage,
    int PageSize,
    IEnumerable<LinkItem> Links
) : CollectionResponse<Linked<TData>>(Data), ICollectionPagedLinks<TData>;