using IHFiction.SharedKernel.Searching;

namespace IHFiction.SharedWeb.Features.Discovery;

public sealed record TagDisplayItem(string Category, string? Subcategory, string Value, string RouteKey)
{
    public static string CanonicalizeCategory(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        var slug = TagRouteSpec.SlugifyAscii(category);
        return string.IsNullOrEmpty(slug) ? category.Trim() : slug;
    }

    public static string? CanonicalizeSubcategory(string? subcategory) =>
        string.IsNullOrWhiteSpace(subcategory) ? null : subcategory.Trim().Replace('_', ' ');

    public static string FormatValue(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().Replace('_', ' ');
    }

    public static bool IsContentWarningCategory(string? category) =>
        !string.IsNullOrWhiteSpace(category)
        && string.Equals(CanonicalizeCategory(category), "content-warning", StringComparison.OrdinalIgnoreCase);

    public static bool IsPrimarySelectionCategory(string? category) =>
        !string.IsNullOrWhiteSpace(category)
        && CanonicalizeCategory(category) is "genre" or "fandom" or "language";

    public static IReadOnlyList<TagCategoryGroup> Group(IEnumerable<TagDisplayItem> tags) =>
        [.. tags
            .Select(tag => new TagDisplayItem(
                CanonicalizeCategory(tag.Category),
                CanonicalizeSubcategory(tag.Subcategory),
                FormatValue(tag.Value),
                tag.RouteKey))
            .OrderBy(tag => tag.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(tag => tag.Subcategory, StringComparer.OrdinalIgnoreCase)
            .ThenBy(tag => tag.Value, StringComparer.OrdinalIgnoreCase)
            .GroupBy(tag => tag.Category, StringComparer.OrdinalIgnoreCase)
            .Select(category => new TagCategoryGroup(
                category.Key,
                [.. category
                    .GroupBy(tag => tag.Subcategory, StringComparer.OrdinalIgnoreCase)
                    .Select(section => new TagSubcategoryGroup(section.Key, [.. section]))]))];

    public static TagDisplayItem FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = text.Split(':', StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            2 => new(CanonicalizeCategory(parts[0]), null, FormatValue(parts[1]), text),
            3 => new(CanonicalizeCategory(parts[0]), CanonicalizeSubcategory(parts[1]), FormatValue(parts[2]), text),
            _ => new("Other", null, FormatValue(text), text)
        };
    }
}

public sealed record TagCategoryGroup(string Category, IReadOnlyList<TagSubcategoryGroup> Sections);

public sealed record TagSubcategoryGroup(string? Subcategory, IReadOnlyList<TagDisplayItem> Tags);
