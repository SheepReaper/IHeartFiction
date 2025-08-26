using Microsoft.AspNetCore.Components;

namespace IHFiction.SharedWeb.Components;

public abstract class CancellableComponent : ComponentBase, IAsyncDisposable
{
    private bool _disposed;
    private readonly CancellationTokenSource _cts = new();

    protected CancellationToken CancellationToken => _cts.Token;

    protected virtual async ValueTask DisposeAsyncCore()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _cts.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        await DisposeAsyncCore().ConfigureAwait(false);

        GC.SuppressFinalize(this);

        _disposed = true;
    }
}