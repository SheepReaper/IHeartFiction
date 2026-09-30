namespace IHFiction.SharedWeb.Components.Tags;

public static class TagInputParser
{
    private static readonly char[] Separators = [',', ';', '\r', '\n'];

    public static IReadOnlyList<string> Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var tags = new List<string>();
        var normalizedKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in input.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalizedKey = Normalize(candidate);
            if (normalizedKey.Length > 0 && normalizedKeys.Add(normalizedKey))
            {
                tags.Add(candidate);
            }
        }

        return tags;
    }

    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.Concat(value.Where(character => character == ':' || char.IsLetterOrDigit(character))
            .Select(char.ToLowerInvariant));
    }
}
