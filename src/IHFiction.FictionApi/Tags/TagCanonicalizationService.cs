using System.Text;

namespace IHFiction.FictionApi.Tags;

internal static class TagCanonicalizationService
{
    public static string BuildKey(string category, string? subcategory, string value)
    {
        var normalizedCategory = NormalizeComponent(category);
        var normalizedSubcategory = string.IsNullOrWhiteSpace(subcategory) ? null : NormalizeComponent(subcategory);
        var normalizedValue = NormalizeComponent(value);

        if (string.IsNullOrWhiteSpace(normalizedCategory) || string.IsNullOrWhiteSpace(normalizedValue))
        {
            return string.Empty;
        }

        return normalizedSubcategory is null
            ? $"{normalizedCategory}:{normalizedValue}"
            : $"{normalizedCategory}:{normalizedSubcategory}:{normalizedValue}";
    }

    public static bool Matches(string categoryA, string? subcategoryA, string valueA, string categoryB, string? subcategoryB, string valueB)
        => string.Equals(BuildKey(categoryA, subcategoryA, valueA), BuildKey(categoryB, subcategoryB, valueB), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeComponent(string component)
    {
        if (string.IsNullOrWhiteSpace(component))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(component.Length);
        var previousWasSeparator = false;

        foreach (var ch in component.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                previousWasSeparator = false;
                continue;
            }

            if ((ch == '_' || ch == '-' || ch == ' ' || ch == '/' || ch == '.') && builder.Length > 0 && !previousWasSeparator)
            {
                previousWasSeparator = true;
            }
        }

        return builder.ToString();
    }
}
