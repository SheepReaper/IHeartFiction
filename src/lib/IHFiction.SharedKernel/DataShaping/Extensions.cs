using System.Dynamic;

namespace IHFiction.SharedKernel.DataShaping;

public static class Extensions
{
    public static ExpandoObject ShapeData<T>(this T data, IDataShapingSupport request) => DataShapingService.ShapeData(data, request?.Fields);
    public static ExpandoObject ShapeData<T>(this T data, string? fields) => DataShapingService.ShapeData(data, fields);
}
