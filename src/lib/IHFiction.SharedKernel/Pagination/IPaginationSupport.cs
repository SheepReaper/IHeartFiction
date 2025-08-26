namespace IHFiction.SharedKernel.Pagination;

public interface IPaginationSupport
{
    int? Page { get; }

    int? PageSize { get; }
}
