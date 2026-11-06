using System.Dynamic;

namespace IHFiction.SharedKernel.DataShaping;

public static class Extensions
{
    public static ExpandoObject ShapeData<T>(this T data, IDataShapingSupport request) => DataShapingService.ShapeData(data, request?.Fields);
    public static ExpandoObject ShapeData<T>(this T data, string? fields) => DataShapingService.ShapeData(data, fields);
    public static IQueryable<T> ShapeProjection<T>(this IQueryable<T> query, IDataShapingSupport? request) => DataShapingService.ShapeProjection(query, request?.Fields);
    public static IQueryable<T> ShapeProjection<T>(this IQueryable<T> query, string? fields) => DataShapingService.ShapeProjection(query, fields);
}
