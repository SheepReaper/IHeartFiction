using System.Text.Json;

namespace IHFiction.SharedWeb.Reporting;

internal static class BrowserReportParser
{
    public const string LegacyCspReportMediaType = "application/csp-report";
    public const string ReportsJsonMediaType = "application/reports+json";

    public static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new BrowserReportJsonConverter() }
    };

    public static IReadOnlyList<BrowserReport> DeserializeReports(string? contentType, string body)
    {
        var mediaType = GetMediaType(contentType);

        if (string.Equals(mediaType, LegacyCspReportMediaType, StringComparison.OrdinalIgnoreCase))
        {
            var report = JsonSerializer.Deserialize<CspReportLegacy>(body, ReportJsonOptions);
            return report?.CspReport is null ? [] : [report.ToModernReport()];
        }

        if (string.Equals(mediaType, ReportsJsonMediaType, StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Deserialize<BrowserReport[]>(body, ReportJsonOptions) ?? [];
        }

        return [];
    }

    public static bool IsSupportedContentType(string? contentType)
    {
        var mediaType = GetMediaType(contentType);
        return string.Equals(mediaType, LegacyCspReportMediaType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(mediaType, ReportsJsonMediaType, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetMediaType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var separatorIndex = contentType.IndexOf(';', StringComparison.Ordinal);
        return separatorIndex < 0
            ? contentType.Trim()
            : contentType[..separatorIndex].Trim();
    }
}
