using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace IHFiction.SharedWeb.Extensions;

public static class LoginLogoutEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapLoginAndLogout(this IEndpointRouteBuilder builder, params string[] authenticationSchemes)
    {
        var group = builder.MapGroup("");

        group.MapGet("login", (string? returnUrl, IHttpContextAccessor context) =>
            TypedResults.Challenge(GetAuthProperties(returnUrl, context.HttpContext))).AllowAnonymous();

        group.MapGet("logout", (string? returnUrl, IHttpContextAccessor context) =>
            TypedResults.SignOut(GetAuthProperties(returnUrl, context.HttpContext), authenticationSchemes));

        return group;
    }

    private static AuthenticationProperties GetAuthProperties(string? returnUrl, HttpContext? context)
    {
        var pathBase = context?.Request.PathBase ?? "/";

        if (string.IsNullOrEmpty(returnUrl))
        {
            returnUrl = pathBase;
        }
        else if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            returnUrl = new Uri(returnUrl, UriKind.Absolute).PathAndQuery;
        }
        else if (returnUrl[0] != '/')
        {
            returnUrl = $"{pathBase}{returnUrl}";
        }

        return new AuthenticationProperties { RedirectUri = returnUrl };
    }
}