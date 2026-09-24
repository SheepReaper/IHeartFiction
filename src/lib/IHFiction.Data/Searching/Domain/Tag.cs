using IHFiction.Data.Stories.Domain;
using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Searching.Domain;

public abstract class Tag : DomainUlidEntityWithTimestamp
{
    public string Category { get; set; } = default!;
    public string? Subcategory { get; set; }
    public string Value { get; set; } = default!;
    public string NormalizedKey { get; private set; } = default!;

    private ICollection<Work>? _works;
    public ICollection<Work> Works => _works ??= [];

    public virtual Tag ResolveCanonical() => this;

    public static CanonicalTag CreateCanonical(string category, string? subcategory, string value)
    {
        var tag = new CanonicalTag();
        tag.Rename(category, subcategory, value);
        return tag;
    }

    public static SynonymTag CreateSynonym(
        CanonicalTag canonicalTag,
        string category,
        string? subcategory,
        string value)
    {
        ArgumentNullException.ThrowIfNull(canonicalTag);

        var tag = new SynonymTag
        {
            CanonicalTag = canonicalTag,
            CanonicalTagId = canonicalTag.Id
        };
        tag.Rename(category, subcategory, value);
        return tag;
    }

    public void Rename(string category, string? subcategory, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Category = category;
        Subcategory = string.IsNullOrWhiteSpace(subcategory) ? null : subcategory;
        Value = value;
        NormalizedKey = BuildNormalizedKey(Category, Subcategory, Value);
    }

    public static string BuildNormalizedKey(string category, string? subcategory, string value)
    {
        static string Normalize(string component) => string.Concat(
            component.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant));

        var components = string.IsNullOrWhiteSpace(subcategory)
            ? new[] { category, value }
            : [category, subcategory, value];

        return string.Join(':', components.Select(Normalize));
    }

    public override string ToString() => Subcategory is null ? $"{Category}:{Value}" : $"{Category}:{Subcategory}:{Value}";
}
