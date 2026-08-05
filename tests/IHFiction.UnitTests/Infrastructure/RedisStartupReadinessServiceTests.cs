using IHFiction.FictionApi.Infrastructure;

using StackExchange.Redis;

namespace IHFiction.UnitTests.Infrastructure;

public sealed class RedisStartupReadinessServiceTests
{
    [Fact]
    public async Task WaitUntilAvailableRetriesTransientConnectionFailures()
    {
        var attempts = 0;

        await RedisStartupReadinessService.WaitUntilAvailableAsync(
            () =>
            {
                attempts++;
                return attempts < 3
                    ? Task.FromException(new RedisConnectionException(
                        ConnectionFailureType.UnableToConnect,
                        "Redis is starting."))
                    : Task.CompletedTask;
            },
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.Equal(3, attempts);
    }
}
