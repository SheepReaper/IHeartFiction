namespace IHFiction.FictionApi.Extensions;

internal static class LoggerExtensions
{
    private static readonly Action<ILogger, string, Exception?> LogValidationException = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(400, "Validaiton Error"),
        "A request validaiton error ocurred: {Message}");
    public static void ValidationError(this ILogger logger, Exception exception)
    {
        LogValidationException(logger, exception.Message, null);
    }
}
