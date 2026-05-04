using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace IHFiction.SharedWeb.Services;

/// <summary>
/// Provides a simplified wrapper around ProtectedLocalStorage with consistent error handling.
/// This service handles prerendering scenarios and circuit disconnections per ASP.NET Core
/// Blazor protected browser storage guidance.
/// </summary>
public sealed class BrowserProtectedStorageService(ProtectedLocalStorage storage)
{
    /// <summary>
    /// Retrieves a value from protected local storage, returning null if not found or unavailable.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var result = await storage.GetAsync<T>(key);
            return result.Success ? result.Value : default;
        }
        catch (InvalidOperationException)
        {
            // Protected storage is not available during prerendering.
            return default;
        }
        catch (JSDisconnectedException)
        {
            // Circuit was disconnected; return default value.
            return default;
        }
    }

    /// <summary>
    /// Stores a value in protected local storage. If value is null, deletes the key instead.
    /// </summary>
    public async Task SetAsync<T>(string key, T value)
    {
        try
        {
            if (value is null)
            {
                await storage.DeleteAsync(key);
                return;
            }

            await storage.SetAsync(key, value);
        }
        catch (InvalidOperationException)
        {
            // Protected storage is not available during prerendering; operation is silently ignored.
        }
        catch (JSDisconnectedException)
        {
            // Circuit was disconnected; operation is silently ignored.
        }
    }

    /// <summary>
    /// Deletes a key from protected local storage.
    /// </summary>
    public async Task DeleteAsync(string key)
    {
        try
        {
            await storage.DeleteAsync(key);
        }
        catch (InvalidOperationException)
        {
            // Protected storage is not available during prerendering; operation is silently ignored.
        }
        catch (JSDisconnectedException)
        {
            // Circuit was disconnected; operation is silently ignored.
        }
    }
}
