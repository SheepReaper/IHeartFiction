namespace IHFiction.SharedWeb.Services;

public sealed class ViewPreferencesService(BrowserProtectedStorageService storage)
{
    private const string CompactViewStorageKey = "viewPreferences:compact";

    public bool IsCompact { get; private set; } = true;

    public event EventHandler<CompactModeChangedEventArgs>? CompactModeChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var persistedValue = await storage.GetAsync<bool?>(CompactViewStorageKey);
        var nextValue = persistedValue ?? true;

        if (IsCompact == nextValue)
        {
            return;
        }

        IsCompact = nextValue;
        CompactModeChanged?.Invoke(this, new(IsCompact));
    }

    public async Task ToggleCompactAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IsCompact = !IsCompact;
        await storage.SetAsync(CompactViewStorageKey, IsCompact);
        CompactModeChanged?.Invoke(this, new(IsCompact));
    }
}

public sealed class CompactModeChangedEventArgs(bool isCompact) : EventArgs
{
    public bool IsCompact => isCompact;
}