using StackExchange.Redis;

namespace IHFiction.FictionApi.Infrastructure;

internal sealed partial class RedisStartupReadinessService(
    IConnectionMultiplexer redis,
    ILogger<RedisStartupReadinessService> logger) : IHostedService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public Task StartAsync(CancellationToken cancellationToken) => WaitUntilAvailableAsync(
        async () => await redis.GetDatabase().PingAsync(),
        RetryDelay,
        cancellationToken,
        exception => LogRedisUnavailable(logger, RetryDelay.TotalSeconds, exception));

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static async Task WaitUntilAvailableAsync(
        Func<Task> probe,
        TimeSpan retryDelay,
        CancellationToken cancellationToken,
        Action<Exception>? onFailure = null)
    {
        while (true)
        {
            try
            {
                await probe();
                return;
            }
            catch (Exception exception) when (exception is RedisConnectionException or RedisTimeoutException)
            {
                onFailure?.Invoke(exception);
                await Task.Delay(retryDelay, cancellationToken);
            }
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Redis is unavailable during Wolverine startup. Retrying in {RetryDelaySeconds} seconds.")]
    private static partial void LogRedisUnavailable(ILogger logger, double retryDelaySeconds, Exception exception);
}
