namespace IHFiction.SharedWeb.Components.Tags;

public sealed record TagDisplayItem(string Category, string? Subcategory, string Value, string Key)
{
    public static IReadOnlyList<TagCategoryGroup> Group(IEnumerable<TagDisplayItem> tags) =>
        [.. tags
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
            2 => new(parts[0], null, parts[1], text),
            3 => new(parts[0], parts[1], parts[2], text),
            _ => new("Other", null, text, text)
        };
    }
}

public sealed record TagCategoryGroup(string Category, IReadOnlyList<TagSubcategoryGroup> Sections);

public sealed record TagSubcategoryGroup(string? Subcategory, IReadOnlyList<TagDisplayItem> Tags);
