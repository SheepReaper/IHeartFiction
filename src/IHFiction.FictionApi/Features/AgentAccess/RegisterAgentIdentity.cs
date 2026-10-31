using System.Text.Json.Serialization;

using IHFiction.FictionApi.Infrastructure;

using Microsoft.Extensions.Options;

using static IHFiction.FictionApi.Features.AgentAccess.AgentAuthProtocol;

namespace IHFiction.FictionApi.Features.AgentAccess;

internal static class RegisterAgentIdentity
{
    private sealed record IdentityRequest(
        string Type,
        [property: JsonPropertyName("assertion_type")] string AssertionType,
        string Assertion);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => nameof(RegisterAgentIdentity);

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            var routes = CreateEndpointGroup(builder);

            return routes.MapPost("/agent/identity", async (
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
            }).ExcludeFromDescription();
        }
    }
}
