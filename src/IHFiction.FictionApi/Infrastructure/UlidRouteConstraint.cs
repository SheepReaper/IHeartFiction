using System.Globalization;

namespace IHFiction.FictionApi.Infrastructure;

internal sealed partial class UlidRouteConstraint : IRouteConstraint
{
    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var val) || val == null) return false;

        var str = Convert.ToString(val, CultureInfo.InvariantCulture);

        return Ulid.TryParse(str, out _);
    }
}