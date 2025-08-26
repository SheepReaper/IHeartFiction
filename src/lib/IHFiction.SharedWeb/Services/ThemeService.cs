using Microsoft.JSInterop;

namespace IHFiction.SharedWeb.Services;

public sealed class ThemeService(IJSRuntime js)
{
    public string CurrentTheme { get; private set; } = "dark"; // Default to dark initially. This will be updated from the client.
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// Initializes the service's state from the actual theme applied by the startup script.
    /// This should be called once when the relevant component is first rendered.
    /// </summary>
    public async Task InitializeThemeAsync(CancellationToken cancellationToken = default)
    {
        var initialTheme = await js.InvokeAsync<string>("theme.getTheme", cancellationToken);
        if (!string.IsNullOrEmpty(initialTheme) && CurrentTheme != initialTheme)
        {
            CurrentTheme = initialTheme;
            // No need to call JS to set the theme, as it's already set by the startup script.
            // We just notify the rest of the Blazor app of the current theme.
            ThemeChanged?.Invoke(this, new(CurrentTheme));
        }
    }

    private async Task SetThemeAsync(string theme, CancellationToken cancellationToken)
    {
        if (CurrentTheme != theme)
        {   
            CurrentTheme = theme;
            await js.InvokeVoidAsync("theme.setTheme", cancellationToken, theme);
            ThemeChanged?.Invoke(this, new(theme));
        }
    }

    public Task ToggleThemeAsync(CancellationToken cancellationToken = default) => SetThemeAsync(CurrentTheme == "light" ? "dark" : "light", cancellationToken);
}

public class ThemeChangedEventArgs(string theme) : EventArgs
{
    public string Theme => theme;
}
