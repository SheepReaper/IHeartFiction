using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.SharedWeb.Extensions;

public static class TaskExtensions
{
    public static async ValueTask<Result<TValue>> HandleApiException<TValue>(this Task<TValue> task)
    {
        try
        {
            var result = await task;
            return result;
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }
    
    public static async ValueTask<Result> HandleApiException(this Task task)
    {
        try
        {
            await task;
            return Result.Success();
        }
        catch (ApiException ex)
        {
            return ex.ToDomainError();
        }
    }
}