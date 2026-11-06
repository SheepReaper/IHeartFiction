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

        group.MapGet("register", (string? returnUrl, IHttpContextAccessor context) =>
            TypedResults.Challenge(CreateRegisterProperties(returnUrl, context.HttpContext))).AllowAnonymous();

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

    public static IEndpointConventionBuilder MapAuthShortcuts(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("");

        group.MapGet("/login", (string? returnUrl, IHttpContextAccessor context) =>
            TypedResults.Challenge(CreateLoginProperties(returnUrl, context.HttpContext))).AllowAnonymous();

        group.MapGet("/signup", (string? returnUrl, IHttpContextAccessor context) =>
            TypedResults.Challenge(CreateRegisterProperties(returnUrl, context.HttpContext))).AllowAnonymous();

        group.MapGet("/register", (string? returnUrl, IHttpContextAccessor context) =>
            TypedResults.Challenge(CreateRegisterProperties(returnUrl, context.HttpContext))).AllowAnonymous();

        return group;
    }

    internal static AuthenticationProperties CreateLogoutProperties(string? returnUrl, HttpContext? context)
    {
        var pathBase = context?.Request.PathBase ?? PathString.Empty;
        var fallback = $"{pathBase}/";
        var destination = fallback;

        if (RedirectHttpResult.IsLocalUrl(returnUrl))
        {
            destination = returnUrl.StartsWith("~/", StringComparison.Ordinal)
                ? $"{pathBase}{returnUrl[1..]}"
                : returnUrl;

            var relative = destination.StartsWith(fallback, StringComparison.OrdinalIgnoreCase) && fallback.Length > 1
                ? destination[fallback.Length..].TrimStart('/')
                : destination.TrimStart('/');

            if (relative.StartsWith("authentication", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("login", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("signup", StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith("register", StringComparison.OrdinalIgnoreCase))
            {
                destination = fallback;
            }
        }

        return new AuthenticationProperties { RedirectUri = destination };
    }

    private static AuthenticationProperties GetAuthProperties(string? returnUrl, HttpContext? context)
    {
        var pathBase = context?.Request.PathBase ?? "/";
        var fallback = pathBase.Value ?? "/";
        if (string.IsNullOrEmpty(fallback))
        {
            fallback = "/";
        }

        if (string.IsNullOrWhiteSpace(returnUrl) || !RedirectHttpResult.IsLocalUrl(returnUrl))
        {
            return new AuthenticationProperties { RedirectUri = fallback };
        }

        var destination = returnUrl.StartsWith("~/", StringComparison.Ordinal)
            ? $"{pathBase}{returnUrl[1..]}"
            : returnUrl;

        var relative = destination.StartsWith(fallback, StringComparison.OrdinalIgnoreCase) && fallback.Length > 1
            ? destination[fallback.Length..].TrimStart('/')
            : destination.TrimStart('/');

        if (relative.StartsWith("authentication", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("login", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("signup", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("register", StringComparison.OrdinalIgnoreCase))
        {
            destination = fallback;
        }

        return new AuthenticationProperties { RedirectUri = destination };
    }

    internal static AuthenticationProperties CreateLoginProperties(string? returnUrl, HttpContext? context)
    {
        var properties = GetAuthProperties(returnUrl, context);
        properties.IsPersistent = true;
        return properties;
    }

    internal static AuthenticationProperties CreateRegisterProperties(string? returnUrl, HttpContext? context)
    {
        var properties = CreateLoginProperties(returnUrl, context);
        properties.Items["kc_action"] = "register";
        properties.Items["prompt"] = "create";
        return properties;
    }
}
