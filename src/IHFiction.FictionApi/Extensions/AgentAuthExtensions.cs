using System.Security.Claims;
using System.Text.Json.Serialization;

using IHFiction.FictionApi.AgentAuth;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.AgentAuth;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace IHFiction.FictionApi.Extensions;

internal static class AgentAuthExtensions
{
    private const string ProtectedResourcePattern = "/.well-known/oauth-protected-resource";
    private const string AuthorizationServerPattern = "/.well-known/oauth-authorization-server";
    private const string JwksPattern = "/.well-known/jwks.json";
    private const string AuthMdPattern = "/auth.md";
    private const string MarkdownContentType = "text/markdown; charset=utf-8";
    private const string DefaultOidcAuthority = "https://auth.iheartfiction.net/realms/fiction";
    private const string IdJagAssertionType = "urn:ietf:params:oauth:token-type:id-jag";
    private const string JwtBearerGrant = "urn:ietf:params:oauth:grant-type:jwt-bearer";
    private const string ClaimGrant = "urn:workos:agent-auth:grant-type:claim";
    private const string RevokedEvent = "https://schemas.workos.com/events/agent/auth/identity/assertion/revoked";
    private static readonly string[] SupportedGrants = [JwtBearerGrant, ClaimGrant];
    private static readonly string[] SupportedScopes = ["agent.read", "profile.read"];
    private static readonly string[] SupportedIdentityTypes = ["identity_assertion"];
    private static readonly string[] SupportedAssertionTypes = [IdJagAssertionType];
    private static readonly string[] SupportedEvents = [RevokedEvent];

    private sealed record IdentityRequest(
        string Type,
        [property: JsonPropertyName("assertion_type")] string AssertionType,
        string Assertion);
    private sealed record ConfirmClaimRequest(string UserCode);

    public static RouteHandlerBuilder MapAgentAuth(this IEndpointRouteBuilder builder)
    {
        MapDiscovery(builder);
        MapProtocol(builder);

        return builder.MapGet(AuthMdPattern, (
            IOptions<BaseUrlOptions> baseUrl,
            IOptions<AgentAuthOptions> agentOptions,
            IConfiguration configuration) =>
        {
            var resource = baseUrl.Value.BaseUrl?.ToString().TrimEnd('/') ?? "https://api.iheartfiction.net";
            var issuer = agentOptions.Value.Issuer?.ToString().TrimEnd('/') ?? resource;
            var authority = configuration["OidcAuthority"] ?? DefaultOidcAuthority;
            return Results.Text(AuthMdContent.Generate(resource, issuer, authority), MarkdownContentType);
        }).ExcludeFromDescription();
    }

    private static void MapDiscovery(IEndpointRouteBuilder builder)
    {
        builder.MapMethods(ProtectedResourcePattern, [HttpMethods.Head], (HttpContext context, IOptions<BaseUrlOptions> options) =>
        {
            var resource = options.Value.BaseUrl?.ToString().TrimEnd('/') ?? string.Empty;
            context.Response.Headers.Link = $"<{resource}{ProtectedResourcePattern}>; rel=\"oauth-protected-resource\"";
            return Results.Ok();
        }).ExcludeFromDescription();

        builder.MapGet(ProtectedResourcePattern, (
            IOptions<BaseUrlOptions> baseUrl,
            IOptions<AgentAuthOptions> agentOptions,
            IConfiguration configuration) =>
        {
            var resource = baseUrl.Value.BaseUrl?.ToString().TrimEnd('/') ?? "https://api.iheartfiction.net";
            var agentIssuer = agentOptions.Value.Issuer?.ToString().TrimEnd('/') ?? resource;
            return Results.Json(new OAuthProtectedResourceMetadata(
                Resource: resource,
                AuthorizationServers: [agentIssuer],
                ScopesSupported: ["agent.read", "profile.read"],
                BearerMethodsSupported: ["header"],
                ResourceDocumentation: $"{resource}/auth.md"));
        }).ExcludeFromDescription();

        builder.MapMethods(AuthorizationServerPattern, [HttpMethods.Head], () => Results.Ok()).ExcludeFromDescription();
        builder.MapGet(AuthorizationServerPattern, (IOptions<AgentAuthOptions> options) =>
        {
            var issuer = options.Value.Issuer!.AbsoluteUri.TrimEnd('/');
            return Results.Json(new
            {
                issuer,
                jwks_uri = $"{issuer}{JwksPattern}",
                token_endpoint = $"{issuer}/oauth2/token",
                revocation_endpoint = $"{issuer}/oauth2/revoke",
                grant_types_supported = SupportedGrants,
                scopes_supported = SupportedScopes,
                agent_auth = new
                {
                    skill = $"{issuer}{AuthMdPattern}",
                    register_uri = $"{issuer}/agent/identity",
                    claim_uri = $"{issuer}/agent/identity/claim",
                    identity_endpoint = $"{issuer}/agent/identity",
                    claim_endpoint = $"{issuer}/agent/identity/claim",
                    events_endpoint = $"{issuer}/agent/event/notify",
                    identity_types_supported = SupportedIdentityTypes,
                    identity_assertion = new
                    {
                        assertion_types_supported = SupportedAssertionTypes,
                        credential_types_supported = SupportedAssertionTypes,
                    },
                    events_supported = SupportedEvents,
                },
            });
        }).ExcludeFromDescription();

        builder.MapGet(JwksPattern, (AgentTokenService tokens) => Results.Json(tokens.CreateJwksDocument()))
            .ExcludeFromDescription();

        builder.MapMethods(AuthMdPattern, [HttpMethods.Head], (HttpContext context) =>
        {
            context.Response.ContentType = MarkdownContentType;
            return Results.Ok();
        }).ExcludeFromDescription();
    }

    private static void MapProtocol(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/agent/identity", async (
            IdentityRequest request,
            AgentRegistrationService registrations,
            IOptions<AgentAuthOptions> options,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            if (!string.Equals(request.Type, "identity_assertion", StringComparison.Ordinal))
                return ProtocolError("invalid_request", "Only identity_assertion registration is supported.");

            try
            {
                var result = await registrations.RegisterAsync(request.AssertionType, request.Assertion, cancellationToken);
                if (result.IdentityAssertion is not null)
                    return Results.Ok(new
                    {
                        registration_id = result.RegistrationId,
                        registration_type = "identity_assertion",
                        identity_assertion = result.IdentityAssertion,
                        assertion_expires = result.AssertionExpires,
                        scopes = SupportedScopes,
                    });

                context.Response.Headers.WWWAuthenticate = "AgentAuth error=\"interaction_required\"";
                return Results.Json(new
                {
                    error = "interaction_required",
                    error_description = "The existing user must confirm this provider identity.",
                    registration_id = result.RegistrationId,
                    registration_type = "identity_assertion",
                    claim_url = "/agent/identity/claim",
                    claim_token = result.ClaimToken,
                    claim_token_expires = result.ClaimExpires,
                    post_claim_scopes = SupportedScopes,
                    claim = new
                    {
                        user_code = result.UserCode,
                        expires_in = (int)options.Value.ClaimLifetime.TotalSeconds,
                        verification_uri = options.Value.ClaimVerificationUri,
                        interval = result.PollIntervalSeconds,
                    },
                }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (AgentProtocolException exception)
            {
                if (exception.StatusCode == StatusCodes.Status401Unauthorized)
                    context.Response.Headers.WWWAuthenticate =
                        $"AgentAuth error=\"{exception.Error}\", max_age=\"{(int)options.Value.ProviderAssertionMaxAge.TotalSeconds}\"";
                return ProtocolError(exception.Error, exception.Message, exception.StatusCode);
            }
        }).RequireRateLimiting("qualified-reads").ExcludeFromDescription();

        builder.MapPost("/agent/identity/claim", () => ProtocolError(
            "invalid_request",
            "identity_assertion registrations receive claim ceremony details from /agent/identity."))
            .ExcludeFromDescription();

        builder.MapPost("/agent/identity/claim/confirm", async (
            ConfirmClaimRequest request,
            ClaimsPrincipal principal,
            AgentRegistrationService registrations,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await registrations.ConfirmAsync(request.UserCode, principal, cancellationToken);
                return Results.Ok();
            }
            catch (AgentProtocolException exception)
            {
                return ProtocolError(exception.Error, exception.Message, exception.StatusCode);
            }
        }).WithName("ConfirmAgentIdentityClaim")
        .WithTags("Agent authentication")
        .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute
        {
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        });

        builder.MapPost("/oauth2/token", async (
            HttpRequest request,
            AgentRegistrationService registrations,
            CancellationToken cancellationToken) =>
        {
            var form = await request.ReadFormAsync(cancellationToken);
            try
            {
                var result = await registrations.ExchangeAsync(
                    form["grant_type"].ToString(),
                    form["assertion"].ToString(),
                    form["claim_token"].ToString(),
                    form["resource"].ToString(),
                    cancellationToken);
                return Results.Json(new
                {
                    access_token = result.AccessToken,
                    token_type = "Bearer",
                    expires_in = result.ExpiresIn,
                    scope = result.Scope,
                    identity_assertion = result.IdentityAssertion,
                    assertion_expires = result.AssertionExpires,
                });
            }
            catch (AgentProtocolException exception)
            {
                return ProtocolError(exception.Error, exception.Message, exception.StatusCode);
            }
        }).DisableAntiforgery().ExcludeFromDescription();

        builder.MapPost("/oauth2/revoke", async (
            HttpRequest request,
            AgentRegistrationService registrations,
            CancellationToken cancellationToken) =>
        {
            var form = await request.ReadFormAsync(cancellationToken);
            await registrations.RevokeTokenAsync(form["token"].ToString(), cancellationToken);
            return Results.Ok();
        }).DisableAntiforgery().ExcludeFromDescription();

        builder.MapPost("/agent/event/notify", async (
            HttpRequest request,
            AgentRegistrationService registrations,
            CancellationToken cancellationToken) =>
        {
            using var reader = new StreamReader(request.Body);
            var securityEventToken = await reader.ReadToEndAsync(cancellationToken);
            try
            {
                await registrations.RevokeRegistrationAsync(securityEventToken, cancellationToken);
                return Results.Accepted();
            }
            catch (AgentProtocolException exception)
            {
                return Results.Json(new { err = exception.Error, description = exception.Message }, statusCode: exception.StatusCode);
            }
        }).ExcludeFromDescription();
    }

    private static IResult ProtocolError(string error, string description, int statusCode = StatusCodes.Status400BadRequest) =>
        Results.Json(new { error, error_description = description }, statusCode: statusCode);

}
