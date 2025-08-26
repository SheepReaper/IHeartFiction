using IHFiction.SharedKernel.Pagination;

namespace IHFiction.FictionApi.Common;

internal interface IPaginationService
{
    PaginationParams CreatePaginationRequest(
        int? page = null,
        int? pageSize = null,
        Action<PaginationOptions>? configureOptions = null);

    Task<PagedCollection<TSource>> ExecutePagedQueryAsync<TSource>(
        IQueryable<TSource> query,
        IPaginationSupport? pagination = null,
        CancellationToken cancellationToken = default);
}
