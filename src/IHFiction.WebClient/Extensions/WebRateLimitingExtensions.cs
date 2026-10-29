using System.Diagnostics.Metrics;
using System.Globalization;
using System.Threading.RateLimiting;
using IHFiction.SharedWeb.Csp;
using IHFiction.WebClient.Infrastructure;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.Options;

namespace IHFiction.WebClient.Extensions;

internal static partial class WebRateLimitingExtensions
{
    internal const string MeterName = "IHFiction.WebClient.RateLimiting";
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Rejections = Meter.CreateCounter<long>("web.rate_limit.rejections");

    public static IServiceCollection AddWebRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WebRateLimitOptions>().Bind(configuration.GetSection("RateLimiting"))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddRateLimiter();
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<WebRateLimitOptions>, IOptionsMonitor<OpenIdConnectOptions>>((options, configured, oidc) =>
        {
            var limits = configured.Value;
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var policy = ResolvePolicy(context, oidc.Get(SecurityExtensions.KeycloakAuthenticationScheme));
                if (policy is null) return RateLimitPartition.GetNoLimiter("exempt");
                var permits = policy switch
                {
                    "authentication" => limits.Authentication,
                    "callbacks" => limits.Callbacks,
                    "connections" => limits.Connections,
                    "reports" => limits.Reports,
                    _ => limits.General
                };
                return RateLimitPartition.GetFixedWindowLimiter($"{policy}:{VisitorContext.Normalize(context.Connection.RemoteIpAddress) ?? "unknown"}", _ =>
                    new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
            });
            options.OnRejected = async (rejected, cancellationToken) =>
            {
                var context = rejected.HttpContext;
                var policy = ResolvePolicy(context, oidc.Get(SecurityExtensions.KeycloakAuthenticationScheme)) ?? "general";
                var seconds = rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 60;
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                context.Response.Headers.CacheControl = "no-store";
                context.Response.ContentType = "text/plain; charset=utf-8";
                Rejections.Add(1, new KeyValuePair<string, object?>("policy", policy));
                context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("IHFiction.WebClient.RateLimiting").Rejected(policy);
                await context.Response.WriteAsync($"Too many requests. Retry after {seconds} seconds.", cancellationToken);
            };
        });
        return services;
    }

    internal static string? ResolvePolicy(HttpContext context, OpenIdConnectOptions oidc)
    {
        // Endpoint routing accepts trailing slashes; they must not bypass tighter policies.
        var path = new PathString(context.Request.Path.Value?.TrimEnd('/'));
        if (context.GetEndpoint()?.Metadata.GetMetadata<StaticAssetDescriptor>() is not null)
            return null;
        if (path == "/health" || path == "/alive" || path == "/uptime")
            return null;
        if (path == oidc.CallbackPath || path == oidc.SignedOutCallbackPath || path == oidc.RemoteSignOutPath)
            return "callbacks";
        if (path == "/authentication/login" || path == "/authentication/logout" || path == "/authentication/refresh")
            return "authentication";
        if (path == CspConstants.CspReportsEndpoint)
            return "reports";
        if (path == "/_blazor/negotiate" || (path == "/_blazor" && context.WebSockets.IsWebSocketRequest))
            return "connections";
        // SignalR polling/sends/cleanup are transport traffic, not new circuits.
        if (path == "/_blazor")
            return null;
        return "general";
    }

    [LoggerMessage(EventId = 411, Level = LogLevel.Warning, Message = "Web request rate limit exceeded for policy {Policy}")]
    private static partial void Rejected(this ILogger logger, string policy);
}
