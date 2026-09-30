namespace IHFiction.SharedWeb.Components.Stories;

internal static class StorySearchQuery
{
    public const string CanonicalParameter = "q";

    public static string Normalize(string? query) =>
        !string.IsNullOrWhiteSpace(query) ? query.Trim() : string.Empty;

    public static void AddCanonical(IDictionary<string, string?> query, string? search)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            query[CanonicalParameter] = search.Trim();
        }
    }
}

