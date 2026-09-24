using System.ComponentModel.DataAnnotations.Schema;

namespace IHFiction.Data.Searching.Domain;

public sealed class SynonymTag : Tag
{
    public Ulid CanonicalTagId { get; set; }
    public CanonicalTag CanonicalTag { get; set; } = default!;

    public override Tag ResolveCanonical() => CanonicalTag;

    [NotMapped]
    public IReadOnlyCollection<Tag> TagFamily => CanonicalTag.TagFamily;
}
