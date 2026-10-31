using IHFiction.FictionApi.Infrastructure;

using static IHFiction.FictionApi.Features.AgentAccess.AgentAuthProtocol;

namespace IHFiction.FictionApi.Features.AgentAccess;

internal static class GetAgentSigningKeys
{
    internal sealed class Endpoint : IEndpoint
    {
        public string Name => nameof(GetAgentSigningKeys);

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet(JwksPattern, (AgentTokenService tokens) => Results.Json(tokens.CreateJwksDocument()))
                .ExcludeFromDescription();
        }
    }
}
