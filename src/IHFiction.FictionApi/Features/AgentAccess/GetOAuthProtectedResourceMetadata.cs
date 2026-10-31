using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.AgentAuth;

using Microsoft.Extensions.Options;

namespace IHFiction.FictionApi.Features.AgentAccess;

internal static class GetOAuthProtectedResourceMetadata
{
    private const string ProtectedResourcePattern = "/.well-known/oauth-protected-resource";

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => nameof(GetOAuthProtectedResourceMetadata);

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            builder.MapMethods(ProtectedResourcePattern, [HttpMethods.Head], (HttpContext context, IOptions<BaseUrlOptions> options) =>
            {
                var resource = options.Value.BaseUrl?.ToString().TrimEnd('/') ?? string.Empty;
                context.Response.Headers.Link = $"<{resource}{ProtectedResourcePattern}>; rel=\"oauth-protected-resource\"";
                return Results.Ok();
            }).ExcludeFromDescription();

            return builder.MapGet(ProtectedResourcePattern, (
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
        }
    }
}
