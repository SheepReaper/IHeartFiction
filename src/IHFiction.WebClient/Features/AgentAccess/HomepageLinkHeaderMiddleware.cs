namespace IHFiction.WebClient.Features.AgentAccess;

using IHFiction.SharedWeb.Configuration;

using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

internal sealed class HomepageLinkHeaderMiddleware(RequestDelegate next)
{
    private const string ApiCatalogRel = "</.well-known/api-catalog>; rel=\"api-catalog\"; type=\"application/linkset+json\"";

    public async Task InvokeAsync(HttpContext context, IOptions<ApiUrlOptions> apiUrlOptions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(apiUrlOptions);

        if (IsEligibleRequest(context.Request))
        {
            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode == StatusCodes.Status200OK)
                {
                    var apiBase = apiUrlOptions.Value.BaseUrl?.ToString().TrimEnd('/') ?? string.Empty;
                    var links = new List<string>(context.Response.Headers.Link.Where(static link => !string.IsNullOrWhiteSpace(link))!);
                    AddIfMissing(links, ApiCatalogRel);
                    AddIfMissing(links, "</.well-known/ai-catalog.json>; rel=\"ai-catalog\"; type=\"application/json\"");
                    AddIfMissing(links, "</.well-known/agent-skills/index.json>; rel=\"agent-skills\"; type=\"application/vnd.agentskills.v0.2.0+json\"");
                    AddIfMissing(links, $"<{apiBase}/.well-known/oauth-protected-resource>; rel=\"oauth-protected-resource\"");
                    AddIfMissing(links, $"<{apiBase}/openapi/v1.json>; rel=\"service-desc\"; type=\"application/vnd.oai.openapi+json\"");
                    AddIfMissing(links, $"<{apiBase}/scalar/v1>; rel=\"service-doc\"; type=\"text/html\"");
                    AddIfMissing(links, $"<{apiBase}/openapi/v1.json>; rel=\"describedby\"; type=\"application/vnd.oai.openapi+json\"");
                    AddIfMissing(links, "</auth.md>; rel=\"describedby\"; type=\"text/markdown\"");

                    if (SupportsMarkdownNegotiation(context))
                    {
                        var pathAndQuery = string.Concat(
                            context.Request.Path.HasValue ? context.Request.Path.Value : "/",
                            context.Request.QueryString.Value);
                        AddIfMissing(links, $"<{pathAndQuery}>; rel=\"alternate\"; type=\"text/markdown\"");
                    }

                    context.Response.Headers.Link = new StringValues([.. links]);
                }

                return Task.CompletedTask;
            });
        }

        await next(context);
    }

    private static void AddIfMissing(List<string> links, string candidate)
    {
        if (!links.Contains(candidate, StringComparer.Ordinal))
        {
            links.Add(candidate);
        }
    }

    private static bool SupportsMarkdownNegotiation(HttpContext context) =>
        HttpMethods.IsGet(context.Request.Method)
        && context.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>() is not null;

    private static readonly PathString[] ExcludedPrefixes =
    [
        new("/_framework"),
        new("/_content"),
        new("/_blazor"),
        new("/.well-known"),
        new("/authentication"),
        new("/csp-report"),
        new("/uptime"),
        new("/robots.txt"),
        new("/sitemap.xml"),
    ];

    private static bool IsEligibleRequest(HttpRequest request) =>
        (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
        && Array.TrueForAll(
            ExcludedPrefixes,
            prefix => !request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}
