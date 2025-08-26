using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;

using IHFiction.SharedWeb.Infrastructure;

namespace IHFiction.SharedWeb.Extensions;

public static class CookieExtensions
{
    public static IServiceCollection ConfigureCookieOidc(this IServiceCollection services, string cookieScheme, string oidcScheme, double? refreshThresholdSeconds = null)
    {
        services.AddSingleton<CookieOidcRefresher>();

        services.AddOptions<CookieAuthenticationOptions>(cookieScheme).Configure<CookieOidcRefresher>((cookieOptions, refresher) =>
            cookieOptions.Events.OnValidatePrincipal = context => refresher.ValidateOrRefreshCookieAsync(context, oidcScheme, refreshThresholdSeconds));

        services.AddOptions<OpenIdConnectOptions>(oidcScheme).Configure(oidcOptions =>
        {
            // From Keycloak perspective, this requests a long refresh token, like a month, so that we may do things on behalf
            // of the user even when they're not logged in. Our app doesn't need to do anything while the user is no longer
            // signed in.
            // oidcOptions.Scope.Add(OpenIdConnectScope.OfflineAccess);

            oidcOptions.SaveTokens = true;
        });

        return services;
    }

    public static AuthenticationBuilder AddCookieWithOidcApiToken(this AuthenticationBuilder builder, string cookieScheme, string oidcSceme, double? refreshThresholdSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddCookie(cookieScheme);

        builder.Services.ConfigureCookieOidc(cookieScheme, oidcSceme, refreshThresholdSeconds);

        return builder;
    }
}