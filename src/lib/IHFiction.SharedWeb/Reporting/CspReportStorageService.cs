using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using IHFiction.Data.BrowserReports.Domain;
using IHFiction.Data.Contexts;

namespace IHFiction.SharedWeb.Reporting;

internal sealed partial class CspReportStorageService(
    FictionDbContext context,
    ILogger<CspReportStorageService> logger,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task StoreAsync(IEnumerable<BrowserReport> reports, CancellationToken cancellationToken)
    {
        foreach (var report in reports)
        {
            if (report is CspViolationReport cspReport)
            {
                await StoreCspViolationAsync(cspReport, cancellationToken);
                continue;
            }

            if (report is not CspViolationReport)
            {
                await StorePayloadReportAsync(report, cancellationToken);
            }
        }
    }

    public static string CreateCspFingerprint(CspViolationReport report)
    {
        var statusCode = report.Body.StatusCode.HasValue ? ((int)report.Body.StatusCode.Value).ToString("D", null) : string.Empty;
        var canonical = string.Join('\u001f',
            NormalizeToken(report.Body.EffectiveDirective),
            NormalizeUrl(report.Body.BlockedUrl, removeQuery: false, removeQueryWhenOriginOnly: true),
            NormalizeUrl(report.Body.DocumentUrl, removeQuery: true, removeQueryWhenOriginOnly: true),
            NormalizeUrl(report.Body.SourceFile, removeQuery: true, removeQueryWhenOriginOnly: true),
            statusCode);

        return Hash(canonical);
    }

    public static string CreatePayloadHash(BrowserReport report)
    {
        return Hash(CanonicalizeReportBody(report));
    }

    public static string CanonicalizeJson(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonicalJson(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string CanonicalizeReportBody(BrowserReport report)
    {
        var element = GetBodyElement(report);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonicalJson(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private async Task StoreCspViolationAsync(CspViolationReport report, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var fingerprint = CreateCspFingerprint(report);

        var existing = await context.CspViolationReports
            .SingleOrDefaultAsync(stored => stored.Fingerprint == fingerprint, cancellationToken);

        if (existing is not null)
        {
            ApplyCspOccurrence(existing, report, now);
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var record = new CspViolationReportRecord
        {
            Fingerprint = fingerprint,
            EffectiveDirective = TrimToNull(report.Body.EffectiveDirective),
            BlockedResource = TrimToNull(report.Body.BlockedUrl),
            DocumentResource = TrimToNull(report.Body.DocumentUrl),
            SourceFile = TrimToNull(report.Body.SourceFile),
            StatusCode = report.Body.StatusCode.HasValue ? (int)report.Body.StatusCode.Value : null,
            Disposition = TrimToNull(report.Body.Disposition),
            OriginalPolicy = TrimToNull(report.Body.OriginalPolicy),
            FirstSeenAt = now,
            LastSeenAt = now,
            OccurrenceCount = 1,
            LastUserAgent = TrimToNull(report.UserAgent),
            LastSample = TrimToNull(report.Body.Sample),
            LastLineNumber = report.Body.LineNumber,
            LastColumnNumber = report.Body.ColumnNumber
        };

        context.CspViolationReports.Add(record);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            LogCspInsertRace(logger, ex);
            context.Entry(record).State = EntityState.Detached;
            existing = await context.CspViolationReports
                .SingleOrDefaultAsync(stored => stored.Fingerprint == fingerprint, cancellationToken);

            if (existing is null)
            {
                throw;
            }

            ApplyCspOccurrence(existing, report, now);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task StorePayloadReportAsync(BrowserReport report, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reportType = TrimToNull(report.Type) ?? "unknown";
        var payloadJson = CanonicalizeJson(GetPayloadElement(report));
        var payloadHash = CreatePayloadHash(report);

        var existing = await context.BrowserReportPayloads
            .SingleOrDefaultAsync(stored => stored.ReportType == reportType && stored.PayloadHash == payloadHash, cancellationToken);

        if (existing is not null)
        {
            ApplyGenericOccurrence(existing, report, now);
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var record = new BrowserReportPayloadRecord
        {
            ReportType = reportType,
            PayloadHash = payloadHash,
            PayloadJson = payloadJson,
            ReportResource = TrimToNull(report.Url.ToString()),
            LastUserAgent = TrimToNull(GetUserAgent(report)),
            FirstSeenAt = now,
            LastSeenAt = now,
            OccurrenceCount = 1
        };
        ApplySummary(record, report);

        context.BrowserReportPayloads.Add(record);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            LogBrowserReportInsertRace(logger, ex);
            context.Entry(record).State = EntityState.Detached;
            existing = await context.BrowserReportPayloads
                .SingleOrDefaultAsync(stored => stored.ReportType == reportType && stored.PayloadHash == payloadHash, cancellationToken);

            if (existing is null)
            {
                throw;
            }

            ApplyGenericOccurrence(existing, report, now);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static void ApplyCspOccurrence(CspViolationReportRecord record, CspViolationReport report, DateTime now)
    {
        record.LastSeenAt = now;
        record.OccurrenceCount++;
        record.LastUserAgent = TrimToNull(report.UserAgent);
        record.LastSample = TrimToNull(report.Body.Sample);
        record.LastLineNumber = report.Body.LineNumber;
        record.LastColumnNumber = report.Body.ColumnNumber;
    }

    private static void ApplyGenericOccurrence(BrowserReportPayloadRecord record, BrowserReport report, DateTime now)
    {
        record.LastSeenAt = now;
        record.OccurrenceCount++;
        record.LastUserAgent = TrimToNull(GetUserAgent(report));
    }

    private static string NormalizeToken(string? value)
    {
        return TrimToNull(value) is { } normalized ? ToLowerInvariant(normalized) : string.Empty;
    }

    private static string NormalizeUrl(string? value, bool removeQuery, bool removeQueryWhenOriginOnly)
    {
        value = TrimToNull(value);
        if (value is null)
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var fragmentIndex = value.IndexOf('#', StringComparison.Ordinal);
            if (fragmentIndex >= 0)
            {
                value = value[..fragmentIndex];
            }

            var queryIndex = value.IndexOf('?', StringComparison.Ordinal);
            if (removeQuery && queryIndex >= 0)
            {
                value = value[..queryIndex];
            }

            return value.Trim();
        }

        var shouldRemoveQuery = removeQuery || (removeQueryWhenOriginOnly && IsOriginOnly(uri));
        var builder = new UriBuilder(uri)
        {
            Scheme = ToLowerInvariant(uri.Scheme),
            Host = ToLowerInvariant(uri.Host),
            Fragment = string.Empty
        };

        if (shouldRemoveQuery)
        {
            builder.Query = string.Empty;
        }

        return builder.Uri.ToString();
    }

    private static bool IsOriginOnly(Uri uri)
    {
        return string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/";
    }

    private static string? TrimToNull(string? value)
    {
        value = value?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static string ToLowerInvariant(string value)
    {
        return string.Create(value.Length, value, static (chars, state) =>
        {
            for (var i = 0; i < state.Length; i++)
            {
                chars[i] = char.ToLowerInvariant(state[i]);
            }
        });
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            default:
                writer.WriteNullValue();
                break;
        }
    }

    private static JsonElement GetPayloadElement(BrowserReport report)
    {
        if (report is GenericBrowserReport genericReport)
        {
            return genericReport.Payload;
        }

        return JsonSerializer.SerializeToElement(report, report.GetType(), BrowserReportParser.ReportJsonOptions);
    }

    private static JsonElement GetBodyElement(BrowserReport report)
    {
        return report switch
        {
            CoepViolationReport coepReport => JsonSerializer.SerializeToElement(coepReport.Body, BrowserReportParser.ReportJsonOptions),
            CrashReport crashReport => JsonSerializer.SerializeToElement(crashReport.Body, BrowserReportParser.ReportJsonOptions),
            DeprecationReport deprecationReport => JsonSerializer.SerializeToElement(deprecationReport.Body, BrowserReportParser.ReportJsonOptions),
            IntegrityViolationReport integrityReport => JsonSerializer.SerializeToElement(integrityReport.Body, BrowserReportParser.ReportJsonOptions),
            InterventionReport interventionReport => JsonSerializer.SerializeToElement(interventionReport.Body, BrowserReportParser.ReportJsonOptions),
            PermissionsPolicyViolationReport permissionsReport => JsonSerializer.SerializeToElement(permissionsReport.Body, BrowserReportParser.ReportJsonOptions),
            _ => GetBodyElementFromPayload(report)
        };
    }

    private static JsonElement GetBodyElementFromPayload(BrowserReport report)
    {
        var payload = GetPayloadElement(report);
        return payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("body", out var body)
            ? body
            : payload;
    }

    private static string? GetUserAgent(BrowserReport report)
    {
        return report switch
        {
            CspViolationReport cspReport => cspReport.UserAgent,
            CrashReport crashReport => crashReport.UserAgent,
            GenericBrowserReport genericReport when genericReport.Payload.TryGetProperty("user_agent", out var userAgent)
                && userAgent.ValueKind == JsonValueKind.String => userAgent.GetString(),
            _ => null
        };
    }

    private static void ApplySummary(BrowserReportPayloadRecord record, BrowserReport report)
    {
        switch (report)
        {
            case CoepViolationReport coepReport:
                record.SummaryKey = TrimToNull(coepReport.Body.Type);
                record.BlockedResource = TrimToNull(coepReport.Body.BlockedUrl.ToString());
                record.Disposition = TrimToNull(coepReport.Body.Disposition);
                break;
            case CrashReport crashReport:
                record.SummaryKey = TrimToNull(crashReport.Body.Reason);
                record.SummaryMessage = TrimToNull(crashReport.Body.VisibilityState);
                break;
            case DeprecationReport deprecationReport:
                record.SummaryKey = TrimToNull(deprecationReport.Body.Id);
                record.SummaryMessage = TrimToNull(deprecationReport.Body.Message);
                ApplySourceLocation(record, deprecationReport.Body.SourceFile, deprecationReport.Body.LineNumber, deprecationReport.Body.ColumnNumber);
                break;
            case IntegrityViolationReport integrityReport:
                record.SummaryKey = TrimToNull(integrityReport.Body.Destination);
                record.BlockedResource = TrimToNull(integrityReport.Body.BlockedUrl);
                record.Disposition = integrityReport.Body.ReportOnly ? "report" : "enforce";
                break;
            case InterventionReport interventionReport:
                record.SummaryKey = TrimToNull(interventionReport.Body.Id);
                record.SummaryMessage = TrimToNull(interventionReport.Body.Message);
                ApplySourceLocation(record, interventionReport.Body.SourceFile, interventionReport.Body.LineNumber, interventionReport.Body.ColumnNumber);
                break;
            case PermissionsPolicyViolationReport permissionsReport:
                record.SummaryKey = TrimToNull(permissionsReport.Body.FeatureId);
                record.SummaryMessage = TrimToNull(permissionsReport.Body.Message);
                record.Disposition = TrimToNull(permissionsReport.Body.Disposition);
                ApplySourceLocation(record, permissionsReport.Body.SourceFile, permissionsReport.Body.LineNumber, permissionsReport.Body.ColumnNumber);
                break;
        }
    }

    private static void ApplySourceLocation(BrowserReportPayloadRecord record, string? sourceFile, int? lineNumber, int? columnNumber)
    {
        record.SourceFile = TrimToNull(sourceFile);
        record.LineNumber = lineNumber;
        record.ColumnNumber = columnNumber;
    }

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Debug,
        Message = "CSP report insert raced with an existing fingerprint; updating existing row instead.")]
    private static partial void LogCspInsertRace(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Debug,
        Message = "Browser report insert raced with an existing payload hash; updating existing row instead.")]
    private static partial void LogBrowserReportInsertRace(ILogger logger, Exception exception);
}
