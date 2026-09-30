using Microsoft.AspNetCore.HttpOverrides;

using IHFiction.FictionApi.AgentAuth;
using IHFiction.FictionApi.Infrastructure;

using Scalar.AspNetCore;

namespace IHFiction.FictionApi.Extensions;

internal static class MiddlewarePipelineExtensions
{
    public static WebApplication UseFictionApiPipeline(this WebApplication app, IConfiguration configuration)
    {
        // Configure middleware pipeline
        app.UseExceptionHandler();
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var problemDetailsService = statusCodeContext.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = statusCodeContext.HttpContext,
                ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = statusCodeContext.HttpContext.Response.StatusCode
                }
            });
        });

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
            app.UseCors();
        }
        else
        {
            // In production we use a reverse proxy that handles TLS termination
            // Not normally needed in development, but Keycloak may behave strangely without additional configuration
            app.UseHttpsRedirection();
        }

        app.UseRequestTimeouts();
        app.UseOutputCache();

        // Configure authentication and authorization
        app.UseMiddleware<OAuthProtectedResourceChallengeMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<AgentReadOnlyScopeMiddleware>();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.UseWhen(
            context => context.Request.Path.StartsWithSegments("/scalar", StringComparison.Ordinal),
            scalar => scalar.Use((context, next) =>
            {
                context.Response.Headers.Append("Content-Security-Policy", "frame-ancestors 'none'");
                return next(context);
            }));

        app.MapOpenApi();

        app.MapScalarApiReference(o =>
        {
            // Initialize authentication if needed
            o.Authentication ??= new();
            o.Authentication.PreferredSecuritySchemes = ["OAuth2"];

            o.AddHttpAuthentication("JWT", scheme => scheme
                .WithDescription("JWT with fiction-api audience."))
            .AddAuthorizationCodeFlow("OAuth2", flow => flow
                .WithClientId("fiction-api-docs")
                .WithSelectedScopes("fiction_api"))
            .WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl);
        });

        // Map endpoints
        app.MapEndpoints();
        app.MapDefaultEndpoints();
        app.MapApiCatalog();
        app.MapAgentAuth();
        app.MapAgentSkills();

        return app;
    }
}
