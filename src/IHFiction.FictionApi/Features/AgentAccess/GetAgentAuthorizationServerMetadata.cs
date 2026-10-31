using IHFiction.FictionApi.Infrastructure;

using Microsoft.Extensions.Options;

using static IHFiction.FictionApi.Features.AgentAccess.AgentAuthProtocol;

namespace IHFiction.FictionApi.Features.AgentAccess;

internal static class GetAgentAuthorizationServerMetadata
{
    private const string AuthorizationServerPattern = "/.well-known/oauth-authorization-server";
    private const string IdJagAssertionType = "urn:ietf:params:oauth:token-type:id-jag";
    private const string JwtBearerGrant = "urn:ietf:params:oauth:grant-type:jwt-bearer";
    private const string ClaimGrant = "urn:workos:agent-auth:grant-type:claim";
    private const string RevokedEvent = "https://schemas.workos.com/events/agent/auth/identity/assertion/revoked";
    private static readonly string[] SupportedGrants = [JwtBearerGrant, ClaimGrant];
    private static readonly string[] SupportedIdentityTypes = ["identity_assertion"];
    private static readonly string[] SupportedAssertionTypes = [IdJagAssertionType];
    private static readonly string[] SupportedEvents = [RevokedEvent];

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => nameof(GetAgentAuthorizationServerMetadata);

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            builder.MapMethods(AuthorizationServerPattern, [HttpMethods.Head], () => Results.Ok()).ExcludeFromDescription();

            return builder.MapGet(AuthorizationServerPattern, (IOptions<AgentAuthOptions> options) =>
            {
                var issuer = options.Value.Issuer!.AbsoluteUri.TrimEnd('/');
                var skill = options.Value.SkillUri!.AbsoluteUri;
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
                        skill,
                        register_uri = $"{issuer}/agent/identity",
                        claim_uri = $"{issuer}/agent/identity/claim",
                        revocation_uri = $"{issuer}/oauth2/revoke",
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
        }
    }
}
