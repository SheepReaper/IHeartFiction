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

                options.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.PreferredUsername;

                if (builder.Environment.IsDevelopment())
                    options.RequireHttpsMetadata = false;

                if (builder.Configuration["OidcAuthority"] is string authority)
                    options.Authority = authority;

                var existingRedirect = options.Events.OnRedirectToIdentityProvider;
                options.Events.OnRedirectToIdentityProvider = async context =>
                {
                    if (existingRedirect is not null)
                    {
                        await existingRedirect(context);
                    }

                    if (context.Properties.Items.TryGetValue("kc_action", out var kcAction) && !string.IsNullOrEmpty(kcAction))
                    {
                        context.ProtocolMessage.SetParameter("kc_action", kcAction);
                    }

                    // Google OAuth strictly rejects prompt=create with "Error 400: invalid_request" when Keycloak
                    // forwards the prompt parameter to upstream identity providers.
                    if (context.Properties.Items.TryGetValue("prompt", out var prompt) &&
                        !string.IsNullOrEmpty(prompt) &&
                        !string.Equals(prompt, "create", StringComparison.OrdinalIgnoreCase))
                    {
                        context.ProtocolMessage.Prompt = prompt;
                    }
                };
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
