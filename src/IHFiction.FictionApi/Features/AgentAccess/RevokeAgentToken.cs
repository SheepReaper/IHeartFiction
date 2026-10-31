using IHFiction.FictionApi.Infrastructure;

using static IHFiction.FictionApi.Features.AgentAccess.AgentAuthProtocol;

namespace IHFiction.FictionApi.Features.AgentAccess;

internal static class RevokeAgentToken
{
    internal sealed class Endpoint : IEndpoint
    {
        public string Name => nameof(RevokeAgentToken);

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            var routes = CreateEndpointGroup(builder);

            return routes.MapPost("/oauth2/revoke", async (
                HttpRequest request,
                AgentRegistrationService registrations,
                CancellationToken cancellationToken) =>
            {
                var form = await request.ReadFormAsync(cancellationToken);
                await registrations.RevokeTokenAsync(form["token"].ToString(), cancellationToken);
                return Results.Ok();
            }).DisableAntiforgery().ExcludeFromDescription();
        }
    }
}
