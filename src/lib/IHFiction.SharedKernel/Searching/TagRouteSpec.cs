using System.Text;

namespace IHFiction.SharedKernel.Searching;

public enum TagRouteSpecError
{
    None,
    Empty,
    TooLong,
    TooManyTags,
    InvalidFormat
}

public sealed record TagRouteToken(
    string RawToken,
    bool IsId,
    Ulid? TagId,
    bool IsFullyQualified,
    string? Category,
    string? Subcategory,
    string Value,
    string? NormalizedKey,
    string NormalizedValue);

public static class TagRouteSpec
{
    public const int MaxLength = 300;
    public const int MaxTags = 3;

    public static bool TryParse(
        string? specification,
        out IReadOnlyList<TagRouteToken> tokens,
        out TagRouteSpecError error)
    {
        tokens = [];
        error = TagRouteSpecError.None;

        if (string.IsNullOrWhiteSpace(specification))
        {
            error = TagRouteSpecError.Empty;
            return false;
        }

        if (specification.Length > MaxLength)
        {
            error = TagRouteSpecError.TooLong;
            return false;
        }

        var rawTokens = specification.Split([',', '+'], StringSplitOptions.TrimEntries);
        if (rawTokens.Length > MaxTags)
        {
            error = TagRouteSpecError.TooManyTags;
            return false;
        }

        if (rawTokens.Length == 0 || rawTokens.Any(string.IsNullOrWhiteSpace))
        {
            error = TagRouteSpecError.InvalidFormat;
            return false;
        }

        var parsed = new List<TagRouteToken>(rawTokens.Length);
        foreach (var rawToken in rawTokens)
        {
            if (!TryParseToken(rawToken, out var token))
            {
                error = TagRouteSpecError.InvalidFormat;
                return false;
            }

            parsed.Add(token);
        }

        tokens = parsed;
        return true;
    }

    public static string CreateRouteToken(
        Ulid tagId,
        string category,
        string? subcategory,
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!IsAscii(category) || !IsAscii(value) || (subcategory is not null && !IsAscii(subcategory)))
        {
            return $"id:{LowerAscii(tagId.ToString())}";
        }

        var components = string.IsNullOrWhiteSpace(subcategory)
            ? new[] { category, value }
            : [category, subcategory, value];

        var slugs = components.Select(SlugifyAscii).ToArray();
        return slugs.Any(string.IsNullOrEmpty)
            ? $"id:{LowerAscii(tagId.ToString())}"
            : string.Join(':', slugs);
    }

    public static string SlugifyAscii(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);
        var needsSeparator = false;
        foreach (var character in value)
        {
            if (character is >= 'A' and <= 'Z')
            {
                if (needsSeparator && builder.Length > 0) builder.Append('-');
                builder.Append(char.ToLowerInvariant(character));
                needsSeparator = false;
            }
            else if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (needsSeparator && builder.Length > 0) builder.Append('-');
                builder.Append(character);
                needsSeparator = false;
            }
            else
            {
                needsSeparator = builder.Length > 0;
            }
        }

        return builder.ToString();
    }

    public static string NormalizeIdentityComponent(string value) => string.Concat(
        value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant));

    public static string BuildCanonicalSpec(IEnumerable<string> routeTokens) => string.Join(',', routeTokens
        .Distinct(StringComparer.Ordinal)
        .OrderBy(token => token, StringComparer.Ordinal));

    public static string BuildPath(string canonicalSpec) => $"/tags/{canonicalSpec}";

    private static bool TryParseToken(string rawToken, out TagRouteToken token)
    {
        token = default!;
        var parts = rawToken.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 3 || parts.Any(string.IsNullOrWhiteSpace)) return false;

        if (parts.Length == 2
            && string.Equals(parts[0], "id", StringComparison.OrdinalIgnoreCase))
        {
            if (!Ulid.TryParse(parts[1], out var id)) return false;
            token = new(rawToken, true, id, true, "id", null, parts[1], null, LowerAscii(parts[1]));
            return true;
        }

        if (parts.Length == 1)
        {
            var normalizedValue = NormalizeIdentityComponent(parts[0]);
            if (string.IsNullOrEmpty(normalizedValue)) return false;
            token = new(rawToken, false, null, false, null, null, parts[0], null, normalizedValue);
            return true;
        }

        var category = parts[0];
        var subcategory = parts.Length == 3 ? parts[1] : null;
        var value = parts[^1];
        var normalizedComponents = string.IsNullOrWhiteSpace(subcategory)
            ? new[] { NormalizeIdentityComponent(category), NormalizeIdentityComponent(value) }
            : [NormalizeIdentityComponent(category), NormalizeIdentityComponent(subcategory), NormalizeIdentityComponent(value)];

        if (normalizedComponents.Any(string.IsNullOrEmpty)) return false;

        token = new(
            rawToken,
            false,
            null,
            true,
            category,
            subcategory,
            value,
            string.Join(':', normalizedComponents),
            normalizedComponents[^1]);
        return true;
    }

    private static bool IsAscii(string value) => value.All(character => character <= 0x7f);

    private static string LowerAscii(string value) => new(value.Select(char.ToLowerInvariant).ToArray());
}
