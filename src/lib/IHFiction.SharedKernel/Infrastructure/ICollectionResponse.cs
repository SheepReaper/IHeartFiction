namespace IHFiction.SharedKernel.Infrastructure;

public interface ICollectionResponse<out TData> : IHandlerResult
{
    IQueryable<TData> Data { get; }
}


