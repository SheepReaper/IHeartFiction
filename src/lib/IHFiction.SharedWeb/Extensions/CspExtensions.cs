using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace IHFiction.SharedWeb.Extensions;

public static partial class CspExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "CSP report: documentUri={documentUri}, blockedUri={blockedUri}, violatedDirective={violatedDirective}, bodyHash={bodyHash}, truncated={truncated}")]
    public static partial void LogCspReport(this ILogger logger, string? documentUri, string? blockedUri, string? violatedDirective, string bodyHash, bool truncated);

    public static IApplicationBuilder UseCsp(this IApplicationBuilder builder) => builder.Use(async (context, next) =>
    {
        string? nonce;

        using (var rng = RandomNumberGenerator.Create())
        {
            var nonceBytes = new byte[32];
            rng.GetBytes(nonceBytes);
            nonce = Convert.ToBase64String(nonceBytes);
        }

        var policy = $@"
            report-to /csp-report;
            base-uri 'self';
            default-src 'self';
            img-src data: https:;
            object-src 'none';
            script-src 'self' 'unsafe-inline' 'nonce-{nonce}';
            script-src-elem 'self' 'nonce-{nonce}';
            script-src-attr 'self' 'unsafe-inline';
            style-src-elem https: chrome-extension: 'self' 'nonce-{nonce}';
            style-src-attr 'self' 'unsafe-inline';
            font-src 'self' data: cdnjs.cloudflare.com www.slant.co;
            connect-src 'self' http: ws: wss:;
            upgrade-insecure-requests;
            frame-ancestors 'self';
            ".ReplaceLineEndings("");

        context.Response.Headers.ContentSecurityPolicy = policy;

        context.Items["CSPNonce"] = nonce;

        await next();
    });

    public static IEndpointConventionBuilder MapCspReportingEndpoint(this IEndpointRouteBuilder builder) => builder.MapPost("/csp-report", async (HttpContext ctx, ILogger logger) =>
    {
        const int maxReportSize = 64 * 1024; // 64KB cap for reports
        string body;
        bool truncated = false;

        // Respect Content-Length when present and avoid reading huge bodies
        if (ctx.Request.ContentLength.HasValue && ctx.Request.ContentLength.Value > maxReportSize)
        {
            using var sr = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            var buffer = new char[maxReportSize];
            var read = await sr.ReadBlockAsync(buffer, 0, buffer.Length);
            body = new string(buffer, 0, read);
            truncated = true;
        }
        else
        {
            using var sr = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            body = await sr.ReadToEndAsync();
            if (body.Length > maxReportSize)
            {
                body = body[..maxReportSize];
                truncated = true;
            }
        }

        // Compute fingerprint for traceability (log this instead of raw content)
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(body));
        var bodyHash = Convert.ToBase64String(hashBytes);

        // Try to parse JSON and extract a small set of safe fields
        string? documentUri = null;
        string? blockedUri = null;
        string? violatedDirective = null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Support both { "csp-report": { ... } } and direct report objects
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("csp-report", out var nested))
                root = nested;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("document-uri", out var v) && v.ValueKind == JsonValueKind.String)
                    documentUri = v.GetString();

                if (root.TryGetProperty("blocked-uri", out var v2) && v2.ValueKind == JsonValueKind.String)
                    blockedUri = v2.GetString();

                if (root.TryGetProperty("violated-directive", out var v3) && v3.ValueKind == JsonValueKind.String)
                    violatedDirective = v3.GetString();
            }
        }
        catch (JsonException)
        {
            // Parsing failed — do not log body. Keep only hash and truncated flag.
        }

        // Log only the safe extracted fields and the fingerprint (no raw user payload)
        logger.LogCspReport(documentUri, blockedUri, violatedDirective, bodyHash, truncated);

        await TypedResults.Ok().ExecuteAsync(ctx);
    });
}