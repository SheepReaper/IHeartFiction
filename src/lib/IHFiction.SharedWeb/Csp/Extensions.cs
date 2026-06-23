using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using IHFiction.SharedWeb.Components.Disqus;
using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Reporting;

using static IHFiction.SharedWeb.Csp.CspConstants;

namespace IHFiction.SharedWeb.Csp;

public static partial class Extensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "CSP report: documentUri={documentUri}, blockedUri={blockedUri}, violatedDirective={violatedDirective}, bodyHash={bodyHash}, truncated={truncated}")]
    public static partial void LogCspReport(this ILogger logger, string? documentUri, string? blockedUri, string? violatedDirective, string bodyHash, bool truncated);

    public const string NonceMiddlewareKey = $"{nameof(CspNonceMiddleware)}_Registered";
    public const string CspMiddlewareKey = $"{nameof(CspPolicyMiddleware)}_Registered";

    public static IServiceCollection AddCspProvider(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var siteUrlOptions = sp.GetRequiredService<IOptions<SiteUrlOptions>>().Value;
            var disqusOptions = sp.GetRequiredService<IOptions<DisqusOptions>>().Value;

            return new CspPolicyProvider(siteUrlOptions, disqusOptions);
        });

        return services;
    }

    public static IApplicationBuilder UseNonce(this IApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if(builder.Properties.ContainsKey(NonceMiddlewareKey))
        {
            return builder;
        }

        builder.Properties[NonceMiddlewareKey] = true;

        builder.UseMiddleware<CspNonceMiddleware>();

        return builder;
    }

    public static IApplicationBuilder UseCsp(this IApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseNonce();

        if(builder.Properties.ContainsKey(CspMiddlewareKey))
        {
            return builder;
        }

        builder.Properties[CspMiddlewareKey] = true;

        builder.UseMiddleware<CspPolicyMiddleware>();

        return builder;
    }

    public static IEndpointConventionBuilder MapCspReportingEndpoint(this IEndpointRouteBuilder builder) =>
        builder.MapPost(CspReportsEndpoint, async (
            HttpContext ctx,
            ILoggerFactory logFac,
            CspReportStorageService storage,
            CancellationToken cancellationToken) =>
    {
        var logger = logFac.CreateLogger("csp-report-handler");

        const int maxReportSize = 64 * 1024; // 64KB cap for reports
        string body;
        bool truncated = false;

        // Respect Content-Length when present and avoid reading huge bodies
        if (ctx.Request.ContentLength.HasValue && ctx.Request.ContentLength.Value > maxReportSize)
        {
            using var sr = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            var buffer = new char[maxReportSize];
            var read = await sr.ReadBlockAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            body = new string(buffer, 0, read);
            truncated = true;
        }
        else
        {
            using var sr = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            body = await sr.ReadToEndAsync(cancellationToken);
            if (body.Length > maxReportSize)
            {
                body = body[..maxReportSize];
                truncated = true;
            }
        }

        // Compute fingerprint for traceability (log this instead of raw content)
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(body));
        var bodyHash = Convert.ToBase64String(hashBytes);

        if (!BrowserReportParser.IsSupportedContentType(ctx.Request.ContentType))
        {
            logger.LogCspReport(null, null, null, bodyHash, truncated);
            await TypedResults.Ok().ExecuteAsync(ctx);
            return;
        }

        try
        {
            var reports = BrowserReportParser.DeserializeReports(ctx.Request.ContentType, body);
            foreach (var reportBody in reports.OfType<CspViolationReport>().Select(report => report.Body))
            {
                logger.LogCspReport(
                    reportBody.DocumentUrl,
                    reportBody.BlockedUrl,
                    reportBody.EffectiveDirective,
                    bodyHash,
                    truncated);
            }

            await storage.StoreAsync(reports, cancellationToken);
        }
        catch (JsonException)
        {
            // Parsing failed — do not log body. Keep only hash and truncated flag.
            logger.LogCspReport(null, null, null, bodyHash, truncated);
        }

        await TypedResults.Ok().ExecuteAsync(ctx);
    });
}
