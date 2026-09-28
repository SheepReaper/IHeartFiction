namespace IHFiction.WebClient.AgentDiscovery;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using IHFiction.SharedWeb.Configuration;

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
                    context.Response.Headers.Link = new StringValues([
                        ApiCatalogRel,
                        $"<{apiBase}/.well-known/oauth-protected-resource>; rel=\"oauth-protected-resource\"",
                        $"<{apiBase}/openapi/v1.json>; rel=\"service-desc\"; type=\"application/vnd.oai.openapi+json\"",
                        $"<{apiBase}/scalar/v1>; rel=\"service-doc\"; type=\"text/html\"",
                        $"<{apiBase}/openapi/v1.json>; rel=\"describedby\"; type=\"application/vnd.oai.openapi+json\"",
                        "</auth.md>; rel=\"describedby\"; type=\"text/markdown\"",
                    ]);
                }

                return Task.CompletedTask;
            });
        }

        await next(context);
    }

    private static bool IsEligibleRequest(HttpRequest request) =>
        (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
        && (request.Path.Equals(new PathString("/"), StringComparison.OrdinalIgnoreCase)
            || request.Path.Equals(PathString.Empty, StringComparison.OrdinalIgnoreCase));
}
