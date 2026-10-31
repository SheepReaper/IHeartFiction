using IHFiction.FictionApi.Infrastructure;

using static IHFiction.FictionApi.Features.AgentAccess.AgentAuthProtocol;

namespace IHFiction.FictionApi.Features.AgentAccess;

internal static class RequestAgentIdentityClaim
{
    internal sealed class Endpoint : IEndpoint
    {
        public string Name => nameof(RequestAgentIdentityClaim);

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            var routes = CreateEndpointGroup(builder);

            return routes.MapPost("/agent/identity/claim", () => ProtocolError(
                "invalid_request",
                "identity_assertion registrations receive claim ceremony details from /agent/identity."))
                .ExcludeFromDescription();
        }
    }
}
