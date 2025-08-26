using Microsoft.Extensions.DependencyInjection;

namespace IHFiction.IntegrationTests;

public abstract class BaseIntegrationTest(IntegrationTestWebAppFactory factory) : IAsyncDisposable
{
    protected readonly IServiceScope _scope = factory.Services.CreateScope();
    // private readonly IntegrationTestWebAppFactory _factory = factory;

    public virtual ValueTask DisposeAsync()
    {
        // Just dispose the scope
        _scope.Dispose();
        GC.SuppressFinalize(this);
        return default;
    }
}
