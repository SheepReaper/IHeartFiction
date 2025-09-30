namespace IHFiction.WebClient;

internal static partial class Program
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "CSP report: {report}")]
    public static partial void LogCspReport(this ILogger logger, string report);
}