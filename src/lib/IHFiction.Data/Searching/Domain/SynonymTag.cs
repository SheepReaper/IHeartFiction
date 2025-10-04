using System.ComponentModel.DataAnnotations.Schema;

namespace IHFiction.Data.Searching.Domain;

internal sealed class SynonymTag : Tag
{
    public Ulid CanonicalTagId { get; set; }
    public CanonicalTag CanonicalTag { get; set; } = default!;

    [NotMapped]
    public IReadOnlyCollection<Tag> TagFamily => CanonicalTag.TagFamily;
}
