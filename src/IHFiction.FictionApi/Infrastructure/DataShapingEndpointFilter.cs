using System.Dynamic;

using IHFiction.SharedKernel.DataShaping;

namespace IHFiction.FictionApi.Infrastructure;

/// <summary>
/// Endpoint filter that automatically applies response data shaping when an endpoint binds an
/// <see cref="IDataShapingSupport"/> request parameter and returns an unshaped value result.
/// </summary>
internal sealed class DataShapingEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var response = await next(context);

        if (response is not IValueHttpResult { Value: { } value and not ExpandoObject })
        {
            return response;
        }

        var shaping = context.Arguments.OfType<IDataShapingSupport>().FirstOrDefault();
        if (shaping is null)
        {
            return response;
        }

        if (response is IStatusCodeHttpResult { StatusCode: StatusCodes.Status200OK })
        {
            return Results.Ok(DataShapingService.ShapeObject(value, shaping.Fields));
        }

        if (response is IStatusCodeHttpResult { StatusCode: StatusCodes.Status201Created })
        {
            var location = response.GetType().GetProperty("Location")?.GetValue(response) as string;
            var shaped = DataShapingService.ShapeObject(value, shaping.Fields);
            return location is not null
                ? Results.Created(new Uri(location, UriKind.RelativeOrAbsolute), shaped)
                : Results.Created(string.Empty, shaped);
        }

        return response;
    }
}
