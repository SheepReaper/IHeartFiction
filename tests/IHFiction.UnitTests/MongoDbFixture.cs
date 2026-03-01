using MongoDB.Driver;

using Testcontainers.MongoDb;

namespace IHFiction.UnitTests;

public sealed class MongoDbFixture : IAsyncLifetime
{
    private MongoDbContainer? _container;
    public MongoClient? Client { get; private set; }
    public bool IsAvailable { get; private set; }
    public string? ConnectionString { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            // Create and start MongoDB container with replica set for transactions
            _container = new MongoDbBuilder("mongo:7.0")
                .WithUsername("root")
                .WithPassword("password")
                .WithReplicaSet("rs0") // Enable replica set for transactions
                .Build();

            // Start container with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await _container.StartAsync(cts.Token);

            ConnectionString = _container.GetConnectionString();

            // Configure client with reasonable timeouts
            var clientSettings = MongoClientSettings.FromConnectionString(ConnectionString);
            clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            clientSettings.ConnectTimeout = TimeSpan.FromSeconds(5);
            clientSettings.SocketTimeout = TimeSpan.FromSeconds(5);

            Client = new MongoClient(clientSettings);

            // Test the connection with retry logic
            var retryCount = 3;
            var connected = false;

            for (int i = 0; i < retryCount && !connected; i++)
            {
                try
                {
                    using var pingCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await Client.GetDatabase("test").RunCommandAsync<object>("{ ping: 1 }", cancellationToken: pingCts.Token);
                    connected = true;
                }
                catch when (i < retryCount - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }

            IsAvailable = connected;

            if (!IsAvailable)
            {
                throw new InvalidOperationException("Failed to connect to MongoDB after retries");
            }
        }
        catch (Exception)
        {
            // If MongoDB fails to start for any reason, mark as unavailable and clean up
            IsAvailable = false;
            await DisposeAsync();

            // Don't throw - allow tests to skip gracefully
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_container != null)
            {
                await _container.StopAsync();
                await _container.DisposeAsync();
            }
        }
        catch
        {
            // Ignore disposal errors
        }
    }
}