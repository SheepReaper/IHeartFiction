using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.AgentAuth;

using Microsoft.Extensions.Options;

namespace IHFiction.FictionApi.Features.AgentAccess;

internal static class GetAgentAuthInstructions
{
    private const string AuthMdPattern = "/auth.md";
    private const string MarkdownContentType = "text/markdown; charset=utf-8";
    private const string DefaultOidcAuthority = "https://auth.iheartfiction.net/realms/fiction";

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => nameof(GetAgentAuthInstructions);

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            builder.MapMethods(AuthMdPattern, [HttpMethods.Head], (HttpContext context) =>
            {
                context.Response.ContentType = MarkdownContentType;
                return Results.Ok();
            }).ExcludeFromDescription();

            return builder.MapGet(AuthMdPattern, (
                IOptions<BaseUrlOptions> baseUrl,
                IOptions<AgentAuthOptions> agentOptions,
                IConfiguration configuration) =>
            {
                var resource = baseUrl.Value.BaseUrl?.ToString().TrimEnd('/') ?? "https://api.iheartfiction.net";
                var issuer = agentOptions.Value.Issuer?.ToString().TrimEnd('/') ?? resource;
                var authority = configuration["OidcAuthority"] ?? DefaultOidcAuthority;
                return Results.Text(AuthMdContent.Generate(resource, issuer, authority), MarkdownContentType);
            }).ExcludeFromDescription();
        }
    }
}
