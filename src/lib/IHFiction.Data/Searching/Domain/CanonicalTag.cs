using System.ComponentModel.DataAnnotations.Schema;

namespace IHFiction.Data.Searching.Domain;

internal sealed class CanonicalTag : Tag
{
    private HashSet<SynonymTag>? _synonyms;
    public IReadOnlyCollection<SynonymTag> Synonyms => _synonyms ??= [];

    [NotMapped]
    public IReadOnlyCollection<Tag> TagFamily => [this, .. Synonyms];
}
