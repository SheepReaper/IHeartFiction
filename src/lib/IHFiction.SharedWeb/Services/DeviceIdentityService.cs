using System.Globalization;

namespace IHFiction.SharedWeb.Services;

public sealed class DeviceIdentityService(BrowserProtectedStorageService storage)
{
    private const string StorageKey = "notifications:device-id";

    public async Task<string?> GetOrCreateAsync()
    {
        var existing = await storage.GetAsync<string>(StorageKey);
        if (!string.IsNullOrWhiteSpace(existing)) return existing;

        var created = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        await storage.SetAsync(StorageKey, created);
        return created;
    }
}
