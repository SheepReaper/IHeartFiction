using IHFiction.FictionApi.Infrastructure;

using static IHFiction.FictionApi.Features.AgentAccess.AgentAuthProtocol;

namespace IHFiction.FictionApi.Features.AgentAccess;

internal static class ProcessAgentSecurityEvent
{
    internal sealed class Endpoint : IEndpoint
    {
        public string Name => nameof(ProcessAgentSecurityEvent);

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            var routes = CreateEndpointGroup(builder);

            return routes.MapPost("/agent/event/notify", async (
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
    }
}
