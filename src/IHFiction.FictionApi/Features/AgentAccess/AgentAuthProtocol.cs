using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;

namespace IHFiction.FictionApi.Features.AgentAccess;

internal static class AgentAuthProtocol
{
    internal const string JwksPattern = "/.well-known/jwks.json";
    internal static readonly string[] SupportedScopes = ["agent.read", "profile.read"];

    internal static RouteGroupBuilder CreateEndpointGroup(IEndpointRouteBuilder routes) =>
        routes.MapGroup(string.Empty)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ApplyDefaultRateLimitPolicy();

    internal static IResult ProtocolError(string error, string description, int statusCode = StatusCodes.Status400BadRequest) =>
        Results.Json(new { error, error_description = description }, statusCode: statusCode);
}
