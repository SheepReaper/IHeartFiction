using IHFiction.FictionApi.Infrastructure;

using static IHFiction.FictionApi.Features.AgentAccess.AgentAuthProtocol;

namespace IHFiction.FictionApi.Features.AgentAccess;

internal static class ExchangeAgentToken
{
    internal sealed class Endpoint : IEndpoint
    {
        public string Name => nameof(ExchangeAgentToken);

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            var routes = CreateEndpointGroup(builder);

            return routes.MapPost("/oauth2/token", async (
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
        }
    }
}
