using System.Dynamic;
using System.Globalization;

using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Extensions;

internal static class ResultExtensions
{
    public static IResult ToResult(this Result result, Func<IResult> successMapper)
    {
        return result.IsSuccess
            ? successMapper.Invoke()
            : result.DomainError.ToProblemDetailsResult();    
    }

    public static IResult ToResult<T>(this Result<T> result, Func<T, IResult> successMapper)
    {
        return result.IsSuccess
            ? successMapper.Invoke(result.Value)
            : result.DomainError.ToProblemDetailsResult();
    }

    public static IResult ToResult<T>(this Result<T> result, Func<ExpandoObject, IResult> successMapper, IDataShapingSupport shapeQuery)
    {
        return result.IsSuccess
            ? successMapper.Invoke(result.Value.ShapeData(shapeQuery))
            : result.DomainError.ToProblemDetailsResult();
    }

    public static IResult ToOkResult(this Result result) =>
        ToResult(result, () => Results.Ok());


    public static IResult ToOkResult<T>(this Result<T> result) =>
        ToResult(result, ok => Results.Ok(ok));
    
    public static IResult ToOkResult<T>(this Result<T> result, IDataShapingSupport shapeQuery) =>
        ToResult(result, shaped => Results.Ok(shaped), shapeQuery);

    public static IResult ToCreatedResult<T>(this Result<T> result, string path) =>
        ToResult(result, created => Results.Created(new Uri(path.ToString(CultureInfo.InvariantCulture), UriKind.Relative), created));

    public static IResult ToCreatedResult<T>(this Result<T> result, string path, IDataShapingSupport shapeQuery) =>
        ToResult(result, shaped => Results.Created(new Uri(path.ToString(CultureInfo.InvariantCulture), UriKind.Relative), shaped), shapeQuery);

    public static IResult ToDeletedResult(this Result result) =>
        ToResult(result, () => Results.NoContent());

    public static IResult ToDeletedResult<T>(this Result<T> result) =>
        ToResult(result, deleted => Results.NoContent());
}
