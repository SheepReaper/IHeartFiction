using System.Text.RegularExpressions;

using IHFiction.SharedWeb.Components.Disqus;
using IHFiction.SharedWeb.Configuration;

using static IHFiction.SharedWeb.Csp.CspConstants;

namespace IHFiction.SharedWeb.Csp;

public sealed partial class CspPolicyProvider
{
    public string ReportingEndpoints { get; }
    public string ReportTo { get; }

    private const string NonceMarker = "{nonce}";

    // Hash for Blazor's framework-owned reconnect UI inline style block.
    // Recompute if Firefox reports style-src-elem violations from _framework/blazor.web.*.js after a Blazor update.
    // private const string BlazorWebHash = "SfuC48/zZyM2iVQsbRmS3bpkry8OrkY1Mxt9WZ6EYU0=";
    private readonly string[] _policySegments;
    private readonly string[] _policyReportOnlySegments;

    public CspPolicyProvider(SiteUrlOptions siteUrlOptions, DisqusOptions disqusOptions)
    {
        ArgumentNullException.ThrowIfNull(siteUrlOptions);
        ArgumentNullException.ThrowIfNull(disqusOptions);

        var baseUrl = siteUrlOptions.BaseUrl?.ToString().TrimEnd('/') ?? string.Empty;
        var reportTo = $"csp-endpoint=\"{baseUrl}{CspReportsEndpoint}\"";
        var reportingEndpoints = $"default=\"{baseUrl}{CspReportsEndpoint}\",{reportTo}";
        var disqusHost = $"{disqusOptions.ShortName}.disqus.com";
        var webSocketHost = $"wss://{siteUrlOptions.BaseUrl?.Host}";

        // Report-only is intentionally looser while testing strict-dynamic with Disqus and
        // Cloudflare Rocket Loader. CSP3 clients ignore https: when strict-dynamic is active;
        // older clients can still use the host fallback.
        var policyTemplate = $@"
            report-uri {CspReportsEndpoint};
            report-to csp-endpoint;
            base-uri 'self';
            default-src 'none';
            connect-src 'self' {webSocketHost} https://links.services.disqus.com https://glitter.services.disqus.com https://referrer.disqus.com;
            font-src 'self' data:;
            form-action 'self';
            frame-src 'self' data: https://disqus.com https://{disqusHost};
            img-src 'self' data: https://referrer.disqus.com https://c.disquscdn.com https://cdn.viglink.com https://www.gravatar.com;
            manifest-src 'self';
            object-src 'none';
            script-src 'self' https: 'nonce-{NonceMarker}' 'strict-dynamic' 'unsafe-inline';
            script-src-elem 'self' 'nonce-{NonceMarker}' 'unsafe-inline' https://c.disquscdn.com https://d-code.liadm.com;
            script-src-attr 'none';
            style-src 'self';
            style-src-elem 'self' 'unsafe-inline' 'nonce-{NonceMarker}' https://c.disquscdn.com;
            style-src-attr 'self' 'unsafe-inline';
            frame-ancestors 'self';
            ";

        var policyReportOnlyTemplate = string.Empty;

        ReportingEndpoints = reportingEndpoints;
        ReportTo = reportTo;

        _policySegments = GetSegments(policyTemplate, NonceMarker);
        _policyReportOnlySegments = GetSegments(policyReportOnlyTemplate, NonceMarker);
    }

    public static string[] GetSegments(string policyTemplate, string marker)
    {
        return ConsecutiveWhitespaceRegex().Replace(policyTemplate, " ").Trim().Split(marker, StringSplitOptions.RemoveEmptyEntries);
    }

    public string RenderPolicy(string nonce)
    {
        return FormatPolicy(nonce, _policySegments);
    }

    public string RenderPolicyReportOnly(string nonce)
    {
        return FormatPolicy(nonce, _policyReportOnlySegments);
    }

    private static string FormatPolicy(string nonce, params string[] segments)
    {
        return segments.Length switch
        {
            0 => string.Empty,
            1 => segments[0],
            2 => string.Concat(segments[0], nonce, segments[1]),
            3 => string.Concat(segments[0], nonce, segments[1], nonce, segments[2]),
            4 => string.Concat(segments[0], nonce, segments[1], nonce, segments[2], nonce, segments[3]),
            _ => string.Join(nonce, segments)
        };
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex ConsecutiveWhitespaceRegex();
}
