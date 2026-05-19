namespace IHFiction.FictionApi.Extensions;

internal static partial class LoggerExtensions
{
    [LoggerMessage(
        EventId = 400,
        Level = LogLevel.Information,
        Message = "A request validation error occurred: {Message}")]
    public static partial void ValidationError(this ILogger logger, string message);
}
