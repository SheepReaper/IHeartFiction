using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using IHFiction.SharedWeb.Extensions;

using Keycloak.AuthServices.Authorization;

namespace IHFiction.WebClient.Extensions;

internal static class SecurityExtensions
{
    public const string KeycloakAuthenticationScheme = "Keycloak";

    public static IHostApplicationBuilder AddSecurityServices(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication(KeycloakAuthenticationScheme)
            .AddKeycloakOpenIdConnect("keycloak", "fiction", KeycloakAuthenticationScheme, options =>
            {
                options.ClientId = "fiction-frontend";
                options.Scope.Add("fiction_api");
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.Resource = "fiction-api";

                options.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.PreferredUsername;

                if (builder.Environment.IsDevelopment())
                    options.RequireHttpsMetadata = false;

                if (builder.Configuration["OidcAuthority"] is string authority)
                    options.Authority = authority;
            })
            .AddCookieWithOidcApiToken(CookieAuthenticationDefaults.AuthenticationScheme, KeycloakAuthenticationScheme, 60);

        builder.Services.AddKeycloakAuthorization(options =>
        {
            options.RoleClaimType = ClaimTypes.Role;
            options.EnableRolesMapping = RolesClaimTransformationSource.All;
            options.Resource = "fiction-api";
        });

        builder.Services.AddAuthorizationBuilder();
        builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
        builder.Services.AddCascadingAuthenticationState();

        return builder;
    }
}
