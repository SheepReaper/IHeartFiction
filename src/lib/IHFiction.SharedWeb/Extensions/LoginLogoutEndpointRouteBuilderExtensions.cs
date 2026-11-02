using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;

using IHFiction.SharedWeb.Infrastructure;

namespace IHFiction.SharedWeb.Extensions;

public static class LoginLogoutEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapLoginAndLogout(this IEndpointRouteBuilder builder, string cookieScheme, string oidcScheme)
    {
        var group = builder.MapGroup("");

        group.MapGet("login", (string? returnUrl, IHttpContextAccessor context) =>
            TypedResults.Challenge(CreateLoginProperties(returnUrl, context.HttpContext))).AllowAnonymous();

        group.MapPost("logout", ([FromForm] string? returnUrl, IHttpContextAccessor context) =>
            TypedResults.SignOut(CreateLogoutProperties(returnUrl, context.HttpContext), [cookieScheme, oidcScheme]));

        group.MapGet("refresh", async Task<IResult> (
            string? returnUrl,
            HttpContext httpContext,
            CookieOidcRefresher refresher,
            CancellationToken cancellationToken) =>
        {
            var authProperties = GetAuthProperties(returnUrl, httpContext);
            var refreshed = await refresher.TryRefreshAuthenticationAsync(httpContext, cookieScheme, oidcScheme, cancellationToken);

            return refreshed
                ? TypedResults.LocalRedirect(authProperties.RedirectUri!)
                : TypedResults.Challenge(authProperties);
        }).AllowAnonymous();

        return group;
    }

    internal static AuthenticationProperties CreateLogoutProperties(string? returnUrl, HttpContext? context)
    {
        var pathBase = context?.Request.PathBase ?? PathString.Empty;
        var destination = $"{pathBase}/";
        if (RedirectHttpResult.IsLocalUrl(returnUrl))
        {
            destination = returnUrl.StartsWith("~/", StringComparison.Ordinal)
                ? $"{pathBase}{returnUrl[1..]}"
                : returnUrl;
        }

        return new AuthenticationProperties { RedirectUri = destination };
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

    internal static AuthenticationProperties CreateLoginProperties(string? returnUrl, HttpContext? context)
    {
        var properties = GetAuthProperties(returnUrl, context);
        properties.IsPersistent = true;
        return properties;
    }
}
