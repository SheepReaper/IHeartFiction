using System.Net;
using System.Net.Mime;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Csp;
using IHFiction.SharedWeb.Extensions;
using IHFiction.SharedKernel.AgentAuth;
using IHFiction.WebClient.AgentDiscovery;
using IHFiction.WebClient.Components;
using IHFiction.WebClient.MarkdownResponses;

using Sidio.Sitemap.Blazor;

namespace IHFiction.WebClient.Extensions;

internal static class MiddlewarePipelineExtensions
{
    private const string ApiCatalogPath = "/.well-known/api-catalog";
    private const string OAuthAuthorizationServerPath = "/.well-known/oauth-authorization-server";
    private const string OpenIdConfigurationPath = "/.well-known/openid-configuration";
    private const string OAuthProtectedResourcePath = "/.well-known/oauth-protected-resource";
    private const string AuthMdPath = "/auth.md";

    public static WebApplication UseWebClientPipeline(this WebApplication app, IConfiguration configuration)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        if (app.Environment.IsProduction())
        {
            string[] trustedProxiesCidr = [.. (configuration["TrustedProxies"]
                ?? throw new InvalidOperationException("TrustedProxies configuration is required in production"))
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

            string[] allowedHosts = [.. (configuration["AllowedHosts"]
                ?? throw new InvalidOperationException("AllowedHosts configuration is required in production"))
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

            ForwardedHeadersOptions options = new()
            {
                ForwardedHeaders = ForwardedHeaders.All,
                ForwardLimit = null,
                AllowedHosts = allowedHosts
            };

            foreach (var cidr in trustedProxiesCidr)
            {
                if (!System.Net.IPNetwork.TryParse(cidr, out var proxy)) continue;
                options.KnownIPNetworks.Add(proxy);
            }

            app.UseForwardedHeaders(options);
        }

        app.UseCsp();
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseMiddleware<MarkdownResponseMiddleware>();
        app.UseMiddleware<HomepageLinkHeaderMiddleware>();

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals(
                "/_content/IHFiction.SharedWeb/js/service-worker.js",
                StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers["Service-Worker-Allowed"] = "/";
            }

            await next();
        });

        app.UseRequestTimeouts();
        app.UseOutputCache();

        app.MapStaticAssets();

        // Redirects and static endpoints
        app.MapGet("/stories/{storyId:ulid}/read", (Ulid storyId) => Results.Redirect($"/read/{storyId}", permanent: true));
        app.MapGet("/stories/{storyId:ulid}/chapters/{chapterId:ulid}", (Ulid storyId, Ulid chapterId) => Results.Redirect($"/read/{chapterId}", permanent: true));

        app.MapGet("/stories/{id}/cover", async Task<IResult> (
            string id,
            FictionApiClient fictionApiClient,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!Ulid.TryParse(id, out _))
                return TypedResults.NotFound();

            if (fictionApiClient is not FictionApiClient client)
                return TypedResults.Problem("Invalid API client registration.", statusCode: StatusCodes.Status500InternalServerError);

            using var response = await client.GetStoryCoverResponseAsync(id, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return TypedResults.NotFound();

            if (response.StatusCode == HttpStatusCode.Forbidden)
                return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

            if (!response.IsSuccessStatusCode)
                return TypedResults.StatusCode((int)response.StatusCode);

            var contentType = response.Content.Headers.ContentType?.ToString() ?? MediaTypeNames.Application.Octet;

            if (response.Headers.ETag is { } etag)
                httpContext.Response.Headers.ETag = etag.ToString();

            if (response.Headers.CacheControl is { } cacheControl)
                httpContext.Response.Headers.CacheControl = cacheControl.ToString();

            if (response.Content.Headers.LastModified is { } lastModified)
                httpContext.Response.Headers.LastModified = lastModified.ToString("R");

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return TypedResults.File(content, contentType);
        });

        app.MapGroup("authentication")
            .MapLoginAndLogout(CookieAuthenticationDefaults.AuthenticationScheme, SecurityExtensions.KeycloakAuthenticationScheme);

        app.MapCspReportingEndpoint();

        app.MapGet("/robots.txt", (IOptions<SiteUrlOptions> siteUrl, HttpContext ctx) =>
        {
            var baseUrl = siteUrl.Value.BaseUrl!.ToString().TrimEnd('/');
            ctx.Response.Headers.CacheControl = "public, max-age=21600, s-maxage=21600";
            var body = $"Sitemap: {baseUrl}/sitemap.xml\n";
            return Results.Text(body, MediaTypeNames.Text.Plain);
        }).CacheOutput("Robots");

        app.MapMethods(ApiCatalogPath, [HttpMethods.Get, HttpMethods.Head], (IOptions<ApiUrlOptions> apiUrl) =>
            Results.Redirect(
                new Uri(apiUrl.Value.BaseUrl!, ApiCatalogPath).ToString(),
                permanent: true,
                preserveMethod: true));

        app.MapMethods(OAuthAuthorizationServerPath, [HttpMethods.Get, HttpMethods.Head], () =>
            Results.Redirect(
                BuildDiscoveryMetadataUri(configuration, OAuthAuthorizationServerPath),
                permanent: true,
                preserveMethod: true));

        app.MapMethods(OpenIdConfigurationPath, [HttpMethods.Get, HttpMethods.Head], () =>
            Results.Redirect(
                BuildDiscoveryMetadataUri(configuration, OpenIdConfigurationPath),
                permanent: true,
                preserveMethod: true));

        app.MapMethods(OAuthProtectedResourcePath, [HttpMethods.Get, HttpMethods.Head], (IOptions<SiteUrlOptions> siteUrl) =>
            Results.Json(CreateProtectedResourceMetadata(siteUrl.Value.BaseUrl!, configuration)));

        app.MapMethods(AuthMdPath, [HttpMethods.Get, HttpMethods.Head], (IOptions<ApiUrlOptions> apiUrl) =>
            Results.Text(
                CreateAuthMd(apiUrl.Value.BaseUrl!, configuration),
                "text/markdown; charset=utf-8"));

        app.UseSitemap();

        app.MapMethods("/uptime", [HttpMethods.Head, HttpMethods.Get], () => Results.Ok());

        app.MapDefaultEndpoints();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode(options => options.ContentSecurityFrameAncestorsPolicy = null) // This is set in the CSP above
            .AddAdditionalAssemblies(typeof(IHFiction.SharedWeb._Imports).Assembly);

        return app;
    }

    internal static string BuildDiscoveryMetadataUri(IConfiguration configuration, string discoveryPath)
    {
        return $"{GetOidcAuthority(configuration)}{discoveryPath}";
    }

    internal static SiteOAuthProtectedResourceMetadata CreateProtectedResourceMetadata(
        Uri siteBaseUrl,
        IConfiguration configuration) =>
        new(
            siteBaseUrl.AbsoluteUri.TrimEnd('/'),
            [GetOidcAuthority(configuration)],
            ["openid", "profile", "fiction_api"]);

    internal static string CreateAuthMd(Uri apiBaseUrl, IConfiguration configuration) =>
        AuthMdContent.Generate(
            apiBaseUrl.AbsoluteUri,
            apiBaseUrl.AbsoluteUri,
            GetOidcAuthority(configuration));

    private static string GetOidcAuthority(IConfiguration configuration) =>
        (configuration["OidcAuthority"]
            ?? throw new InvalidOperationException("OidcAuthority configuration is required for identity discovery."))
        .TrimEnd('/');

    internal sealed record SiteOAuthProtectedResourceMetadata(
        [property: JsonPropertyName("resource")] string Resource,
        [property: JsonPropertyName("authorization_servers")] IReadOnlyList<string> AuthorizationServers,
        [property: JsonPropertyName("scopes_supported")] IReadOnlyList<string> ScopesSupported);
}
