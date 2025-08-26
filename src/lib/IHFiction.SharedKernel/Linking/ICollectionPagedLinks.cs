using IHFiction.SharedKernel.Pagination;

namespace IHFiction.SharedKernel.Linking;

public interface ICollectionPagedLinks<TData> : ICollectionPaged<Linked<TData>>, ILinks;
