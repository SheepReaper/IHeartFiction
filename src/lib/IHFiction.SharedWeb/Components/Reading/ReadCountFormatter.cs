using System.Globalization;

namespace IHFiction.SharedWeb.Components.Reading;

public static class ReadCountFormatter
{
    public static string Format(int count) => count switch
    {
        < 1_000 => count.ToString("N0", CultureInfo.CurrentCulture),
        < 1_000_000 => $"{count / 1_000d:0.#}K",
        _ => $"{count / 1_000_000d:0.#}M"
    };
}
