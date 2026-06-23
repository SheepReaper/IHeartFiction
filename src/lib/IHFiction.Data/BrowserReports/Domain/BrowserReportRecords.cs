using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.BrowserReports.Domain;

public sealed class CspViolationReportRecord : DomainUlidEntity
{
    public string Fingerprint { get; set; } = default!;
    public string? EffectiveDirective { get; set; }
    public string? BlockedResource { get; set; }
    public string? DocumentResource { get; set; }
    public string? SourceFile { get; set; }
    public int? StatusCode { get; set; }
    public string? Disposition { get; set; }
    public string? OriginalPolicy { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public int OccurrenceCount { get; set; }
    public string? LastUserAgent { get; set; }
    public string? LastSample { get; set; }
    public int? LastLineNumber { get; set; }
    public int? LastColumnNumber { get; set; }
}

public sealed class BrowserReportPayloadRecord : DomainUlidEntity
{
    public string ReportType { get; set; } = default!;
    public string PayloadHash { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public string? ReportResource { get; set; }
    public string? SummaryKey { get; set; }
    public string? SummaryMessage { get; set; }
    public string? BlockedResource { get; set; }
    public string? SourceFile { get; set; }
    public int? LineNumber { get; set; }
    public int? ColumnNumber { get; set; }
    public string? Disposition { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public int OccurrenceCount { get; set; }
    public string? LastUserAgent { get; set; }
}
