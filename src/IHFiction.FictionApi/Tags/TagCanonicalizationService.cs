using IHFiction.Data.Searching.Domain;

namespace IHFiction.FictionApi.Tags;

internal static class TagCanonicalizationService
{
    public static string BuildKey(string category, string? subcategory, string value)
        => Tag.BuildNormalizedKey(category, subcategory, value);

    public static bool Matches(string categoryA, string? subcategoryA, string valueA, string categoryB, string? subcategoryB, string valueB)
        => string.Equals(BuildKey(categoryA, subcategoryA, valueA), BuildKey(categoryB, subcategoryB, valueB), StringComparison.OrdinalIgnoreCase);
}
