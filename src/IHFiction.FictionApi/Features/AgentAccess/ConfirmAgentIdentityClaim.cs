using IHFiction.FictionApi.Common;
using System.Security.Claims;

using IHFiction.FictionApi.Infrastructure;

using Microsoft.AspNetCore.Authentication.JwtBearer;

using static IHFiction.FictionApi.Features.AgentAccess.AgentAuthProtocol;

namespace IHFiction.FictionApi.Features.AgentAccess;

internal static class ConfirmAgentIdentityClaim
{
    private sealed record ConfirmClaimRequest(string UserCode);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => nameof(ConfirmAgentIdentityClaim);

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            var routes = CreateEndpointGroup(builder);

            return routes.MapPost("/agent/identity/claim/confirm", async (
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
            .WithTags(ApiTags.AgentAuthentication)
            .Produces(StatusCodes.Status200OK)
            .RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute
            {
                AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
            });
        }
    }
}
