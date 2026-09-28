using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using IHFiction.Data.AgentAuth.Domain;
using IHFiction.Data.Contexts;
using IHFiction.FictionApi.AgentAuth;

using Keycloak.AuthServices.Authorization;

namespace IHFiction.FictionApi.Extensions;

internal static class SecurityExtensions
{
    private const string BearerSelectorScheme = "BearerSelector";

    public static IHostApplicationBuilder AddSecurityServices(
        this IHostApplicationBuilder builder,
        bool isBuildEnvironment)
    {
        builder.Services.AddOptions<AgentAuthOptions>()
            .Bind(builder.Configuration.GetSection(AgentAuthOptions.SectionName))
            .PostConfigure(options =>
            {
                if (options.Issuer is null
                    && Uri.TryCreate(builder.Configuration["BaseUrl"], UriKind.Absolute, out var issuer))
                    options.Issuer = issuer;
            })
            .Validate(options => options.Issuer is { IsAbsoluteUri: true }, "AgentAuth:Issuer must be an absolute URI.")
            .Validate(options => options.ClaimVerificationUri is { IsAbsoluteUri: true }, "AgentAuth:ClaimVerificationUri must be an absolute URI.")
            .Validate(options => options.AccessTokenLifetime > TimeSpan.Zero && options.AccessTokenLifetime <= TimeSpan.FromHours(1),
                "AgentAuth:AccessTokenLifetime must be greater than zero and no longer than one hour.")
            .Validate(options => options.ServiceAssertionLifetime > TimeSpan.Zero && options.ServiceAssertionLifetime <= TimeSpan.FromMinutes(15),
                "AgentAuth:ServiceAssertionLifetime must be greater than zero and no longer than 15 minutes.")
            .Validate(options => options.ProviderAssertionMaxAge > TimeSpan.Zero && options.ClaimLifetime > TimeSpan.Zero,
                "Agent assertion and claim freshness limits must be greater than zero.")
            .Validate(options => options.TrustedProviders.All(provider =>
                    provider.Issuer is { IsAbsoluteUri: true }
                    && provider.JwksUri is { IsAbsoluteUri: true }
                    && provider.ClientIds.Count > 0
                    && provider.SigningAlgorithms.Count > 0),
                "Each trusted agent provider must configure an issuer, JWKS URI, client ID, and signing algorithm.")
            .Validate(options => options.PreviousVerificationKeys.All(key =>
                    !string.IsNullOrWhiteSpace(key.KeyId)
                    && !string.IsNullOrWhiteSpace(key.PublicKeyPem))
                && options.PreviousVerificationKeys.Select(key => key.KeyId)
                    .Append(options.SigningKeyId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == options.PreviousVerificationKeys.Count + 1,
                "Agent signing and previous verification key IDs must be unique and contain key material.")
            .ValidateOnStart();

        builder.Services.AddAuthentication(BearerSelectorScheme)
            .AddPolicyScheme(BearerSelectorScheme, BearerSelectorScheme, options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authorization = context.Request.Headers.Authorization.ToString();
                    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        return JwtBearerDefaults.AuthenticationScheme;

                    try
                    {
                        var token = new JsonWebTokenHandler().ReadJsonWebToken(authorization["Bearer ".Length..]);
                        var agentIssuer = context.RequestServices.GetRequiredService<IOptions<AgentAuthOptions>>()
                            .Value.Issuer?.AbsoluteUri.TrimEnd('/');
                        return string.Equals(token.Issuer.TrimEnd('/'), agentIssuer, StringComparison.Ordinal)
                            ? AgentTokenService.AuthenticationScheme
                            : JwtBearerDefaults.AuthenticationScheme;
                    }
                    catch (ArgumentException)
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }
                };
            })
            .AddKeycloakJwtBearer("keycloak", realm: "fiction", JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Audience = "fiction-api";
                options.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.PreferredUsername;

                if (builder.Environment.IsDevelopment())
                    options.RequireHttpsMetadata = false;

                // Allow explicit authority override from configuration (e.g. to force HTTP endpoint in development)
                if (builder.Configuration["OidcAuthority"] is string authority)
                    options.Authority = authority;

                // Allow for a small clock drift between the API and the identity provider
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);
            })
            .AddJwtBearer(AgentTokenService.AuthenticationScheme, _ => { })
            // This is here to make configuring the docs client easier
            .AddKeycloakOpenIdConnect("keycloak", "fiction", OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = "fiction-api-docs";
                options.Scope.Add("fiction_api");
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.Resource = "fiction-api";

                options.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.PreferredUsername;

                if (builder.Environment.IsDevelopment() || isBuildEnvironment)
                {
                    options.RequireHttpsMetadata = false;
                }

                if (builder.Configuration["OidcAuthority"] is string authority)
                    options.Authority = authority;
            });

        builder.Services.AddOptions<JwtBearerOptions>(AgentTokenService.AuthenticationScheme)
            .Configure<AgentTokenService>((options, tokens) =>
            {
                options.TokenValidationParameters = tokens.CreateValidationParameters();
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var jti = principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                        var registrationValue = principal?.FindFirstValue(AgentTokenService.RegistrationIdClaim);
                        if (principal?.FindFirstValue(AgentTokenService.TokenKindClaim) != AgentTokenService.AccessTokenKind
                            || string.IsNullOrWhiteSpace(jti)
                            || !Ulid.TryParse(registrationValue, out var registrationId))
                        {
                            context.Fail("The bearer token is not an agent access token.");
                            return;
                        }

                        var db = context.HttpContext.RequestServices.GetRequiredService<FictionDbContext>();
                        var valid = await db.AgentAccessTokens.AnyAsync(
                            token => token.TokenJti == jti
                                && token.RegistrationId == registrationId
                                && token.RevokedAt == null
                                && token.ExpiresAt > DateTime.UtcNow,
                            context.HttpContext.RequestAborted);
                        var registrationActive = await db.AgentRegistrations.AnyAsync(
                            registration => registration.Id == registrationId
                                && registration.Status == AgentRegistrationStatus.Active
                                && registration.RevokedAt == null,
                            context.HttpContext.RequestAborted);
                        if (!valid || !registrationActive)
                            context.Fail("The agent token has been revoked or its registration is inactive.");
                    },
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("author", p => p.RequireRole("author"))
            .AddPolicy("admin", p => p.RequireRole("admin"));

        builder.Services.AddKeycloakAuthorization(options =>
        {
            options.RoleClaimType = ClaimTypes.Role;
            options.EnableRolesMapping = RolesClaimTransformationSource.All;
            options.Resource = "fiction-api";
        });

        builder.Services.AddKeycloakRealmAdminClient(
            "keycloak",
            clientId: "fiction-admin-client",
            realm: "fiction");

        return builder;
    }
}
