using Microsoft.AspNetCore.Components;

namespace IHFiction.SharedWeb.Components;

public abstract class CancellableComponent : ComponentBase, IAsyncDisposable, IDisposable
{
    private int _disposed;
    private readonly CancellationTokenSource _cts = new();

    protected CancellationToken CancellationToken => _cts.Token;

    protected virtual async ValueTask DisposeAsyncCore()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _cts.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;

        await DisposeAsyncCore().ConfigureAwait(false);

        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;

        if (disposing)
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}