using System.ComponentModel.DataAnnotations.Schema;

namespace IHFiction.Data.Searching.Domain;

internal sealed class SynonymTag : Tag
{
    public Ulid CanonicalTagId { get; set; }
    public required CanonicalTag CanonicalTag { get; set; }

    [NotMapped]
    public IReadOnlyCollection<Tag> TagFamily => CanonicalTag.TagFamily;
}
