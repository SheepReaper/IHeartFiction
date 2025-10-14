using Mongo2Go;

using MongoDB.Driver;

namespace IHFiction.UnitTests;

public sealed class MongoDbFixture : IAsyncLifetime
{
    public MongoDbRunner Runner { get; }
    public MongoClient Client { get; }
    private bool _disposed;

    public MongoDbFixture()
    {
        Runner = MongoDbRunner.Start(singleNodeReplSet: true);
        Client = new MongoClient(Runner.ConnectionString);
    }

    public ValueTask InitializeAsync()
    {
        // No initialization required
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;

        Runner.Dispose();

        _disposed = true;

        return ValueTask.CompletedTask;
    }
}