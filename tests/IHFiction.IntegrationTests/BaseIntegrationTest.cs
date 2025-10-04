using Microsoft.Extensions.DependencyInjection;

namespace IHFiction.IntegrationTests;

public abstract class BaseIntegrationTest(IntegrationTestWebAppFactory factory) : IAsyncDisposable
{
    private bool _disposed;

    protected readonly IServiceScope _scope = factory.Services.CreateScope();
    // private readonly IntegrationTestWebAppFactory _factory = factory;

    protected virtual ValueTask DisposeAsyncCore()
    {
        // Just dispose the scope
        _scope.Dispose();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await DisposeAsyncCore().ConfigureAwait(false);

        GC.SuppressFinalize(this);

        _disposed = true;
    }
}
