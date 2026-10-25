using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

using IHFiction.FictionApi.Infrastructure;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace IHFiction.FictionApi.Extensions;

internal static partial class RateLimitingExtensions
{
    private const int PublicReadsPermits = 120;
    private const int AuthenticatedReadsPermits = 240;
    private const int MutationsPermits = 30;
    private const int AuthPermits = 10;

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Safe default: every request is limited by public-reads unless its endpoint explicitly
            // selects (or disables) a policy, in which case the endpoint's policy is the only limiter.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                HasExplicitPolicy(context.GetEndpoint()?.Metadata)
                    ? RateLimitPartition.GetNoLimiter("explicit-policy")
                    : FixedWindow($"ip:{GetClientIp(context)}", PublicReadsPermits));

            options.AddPolicy(RateLimitPolicies.PublicReads, context =>
                FixedWindow($"ip:{GetClientIp(context)}", PublicReadsPermits));
            options.AddPolicy(RateLimitPolicies.QualifiedReads, context =>
                FixedWindow($"ip:{GetClientIp(context)}", PublicReadsPermits));
            options.AddPolicy(RateLimitPolicies.AuthenticatedReads, context =>
                FixedWindow(GetIdentityKey(context), AuthenticatedReadsPermits));
            options.AddPolicy(RateLimitPolicies.Mutations, context =>
                FixedWindow(GetIdentityKey(context), MutationsPermits));
            options.AddPolicy(RateLimitPolicies.Auth, context =>
                FixedWindow($"ip:{GetClientIp(context)}", AuthPermits));

            options.OnRejected = async (rejected, cancellationToken) =>
            {
                var context = rejected.HttpContext;
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                int? retryAfterSeconds = null;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                    context.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture);
                }

                context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("IHFiction.FictionApi.RateLimiting")
                    .RateLimitRejected(
                        context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? string.Empty,
                        context.Request.Method);

                var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = retryAfterSeconds is { } seconds
                        ? $"Rate limit exceeded. Retry after {seconds} seconds."
                        : "Rate limit exceeded. Retry later.",
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                };
                // The shared ProblemDetails service owns request/trace enrichment.
                await Results.Problem(problem).ExecuteAsync(context);
            };
        });

        return services;
    }

    /// <summary>
    /// Assigns a policy to endpoints that have not chosen one: writes use <c>mutations</c>, authorized reads use
    /// <c>authenticated-reads</c>; anything else falls through to the global <c>public-reads</c> default.
    /// Runs after the endpoint is fully built so explicit <c>RequireRateLimiting</c> calls always win.
    /// </summary>
    public static TBuilder ApplyDefaultRateLimitPolicy<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Finally(endpointBuilder =>
        {
            var policy = ResolveDefaultPolicy(endpointBuilder.Metadata);
            if (policy is not null)
                endpointBuilder.Metadata.Add(new EnableRateLimitingAttribute(policy));
        });

        return builder;
    }

    internal static string? ResolveDefaultPolicy(IEnumerable<object>? metadata)
    {
        if (metadata is null || HasExplicitPolicy(metadata))
            return null;

        var items = metadata as IList<object> ?? [.. metadata];

        var isRead = items.OfType<HttpMethodMetadata>()
            .All(m => m.HttpMethods.All(method =>
                HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)));

        if (!isRead)
            return RateLimitPolicies.Mutations;

        return items.OfType<IAuthorizeData>().Any() && !items.OfType<IAllowAnonymous>().Any()
            ? RateLimitPolicies.AuthenticatedReads
            : null;
    }

    private static bool HasExplicitPolicy(IEnumerable<object>? metadata) =>
        metadata is not null && metadata.Any(m => m is EnableRateLimitingAttribute or DisableRateLimitingAttribute);

    private static string GetClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string GetIdentityKey(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var subject = user.FindFirstValue("sub")
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("client_id")
                ?? user.FindFirstValue("azp");

            if (!string.IsNullOrEmpty(subject))
                return $"sub:{subject}";
        }

        return $"ip:{GetClientIp(context)}";
    }

    private static RateLimitPartition<string> FixedWindow(string key, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });

    [LoggerMessage(
        EventId = 401,
        Level = LogLevel.Warning,
        Message = "Rate limit exceeded for {Endpoint} ({Method})")]
    private static partial void RateLimitRejected(this ILogger logger, string endpoint, string method);
}
