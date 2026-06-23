using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IHFiction.SharedWeb.Reporting;

internal abstract record BrowserReport(
    string Type,
    Uri Url);

internal sealed record CspViolationReport(
    CspReportBody Body,
    string Type,
    Uri Url,
    int? Age,
    [property: JsonPropertyName("user_agent")]
    string? UserAgent)
    : BrowserReport(Type, Url);

internal sealed record GenericBrowserReport(
    string Type,
    Uri Url,
    JsonElement Payload)
    : BrowserReport(Type, Url);

internal sealed record CoepViolationReport(
    CoepReportBody Body,
    string Type,
    Uri Url
) : BrowserReport(Type, Url);

internal sealed record CoepReportBody(
    string Type, // "corp", "navigation", or "worker initialization"
    [property: JsonPropertyName("blockedURL")]
    Uri BlockedUrl,
    string? Destination, // possible values: https://developer.mozilla.org/en-US/docs/Web/API/Request/destination#value
    string Disposition // "enforce" or "reporting"
);

internal sealed record CrashReport(
    int Age,
    string Type,
    Uri Url,
    [property: JsonPropertyName("user_agent")]
    string UserAgent,
    CrashReportBody Body
) : BrowserReport(Type, Url);

internal sealed record CrashReportBody(
    [property: JsonPropertyName("crash_report_api")]
    Dictionary<string, string>? CrashReportApi,
    [property: JsonPropertyName("is_top_level")]
    bool IsTopLevel,
    string? Reason, // "oom" or "unresponsive"
    string? Stack,
    [property: JsonPropertyName("visibility_state")]
    string VisibilityState // "visible" or "hidden"
);

internal sealed record DeprecationReport(
    DeprecationReportBody Body,
    string Type,
    Uri Url
) : BrowserReport(Type, Url);

internal sealed record DeprecationReportBody(
    string Id,
    DateTimeOffset AnticipatedRemoval,
    string Message,
    string? SourceFile,
    int? LineNumber,
    int? ColumnNumber
);

internal sealed record IntegrityViolationReport(
    IntegrityViolationReportBody Body,
    string Type,
    Uri Url
) : BrowserReport(Type, Url);

internal sealed record IntegrityViolationReportBody(
    [property: JsonPropertyName("blockedURL")]
    string BlockedUrl,
    [property: JsonPropertyName("documentURL")]
    string DocumentUrl,
    string Destination, // only "script"
    bool ReportOnly
);

internal sealed record InterventionReport(
    InterventionReportBody Body,
    string Type,
    Uri Url
) : BrowserReport(Type, Url);

internal sealed record InterventionReportBody(
    int? ColumnNumber,
    string Id,
    int? LineNumber,
    string Message,
    string? SourceFile
);

internal sealed record PermissionsPolicyViolationReport(
    PermissionsPolicyViolationReportBody Body,
    string Type,
    Uri Url
) : BrowserReport(Type, Url);

internal sealed record PermissionsPolicyViolationReportBody(
    int? ColumnNumber,
    string Disposition, // "enforce" or "report"
    string FeatureId,
    int? LineNumber,
    string Message,
    string? SourceFile
);

internal sealed record CspReportBody(
    [property: JsonPropertyName("blockedURL")]
    string? BlockedUrl,
    int? ColumnNumber,
    string? Disposition,
    [property: JsonPropertyName("documentURL")]
    string? DocumentUrl,
    string? EffectiveDirective,
    int? LineNumber,
    string? OriginalPolicy,
    string? Referrer,
    string? Sample,
    string? SourceFile,
    HttpStatusCode? StatusCode);

internal sealed record CspReportLegacyBody(
    [property: JsonPropertyName("blocked-uri")]
    string? BlockedUri,
    [property: JsonPropertyName("column-number")]
    int? ColumnNumber,
    string? Disposition,
    [property: JsonPropertyName("document-uri")]
    string DocumentUri,
    [property: JsonPropertyName("effective-directive")]
    string? EffectiveDirective,
    [property: JsonPropertyName("line-number")]
    int? LineNumber,
    [property: JsonPropertyName("original-policy")]
    string? OriginalPolicy,
    string? Referrer,
    [property: JsonPropertyName("script-sample")]
    string? ScriptSample,
    [property: JsonPropertyName("source-file")]
    string? SourceFile,
    [property: JsonPropertyName("status-code")]
    HttpStatusCode? StatusCode)
{
    public CspReportBody ToReportBody() => new(
        BlockedUri,
        ColumnNumber,
        Disposition,
        DocumentUri,
        EffectiveDirective,
        LineNumber,
        OriginalPolicy,
        Referrer,
        ScriptSample,
        SourceFile,
        StatusCode);
}

internal sealed record CspReportLegacy(
    [property: JsonPropertyName("csp-report")]
    CspReportLegacyBody CspReport)
{
    public CspViolationReport ToModernReport() => new(CspReport.ToReportBody(), "csp-violation", new(CspReport.DocumentUri), null, null);
}
