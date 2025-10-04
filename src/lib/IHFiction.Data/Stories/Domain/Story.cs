using MongoDB.Bson;

namespace IHFiction.Data.Stories.Domain;

public sealed class Story : Work
{
    public string Description { get; set; } = default!;

    public ObjectId? WorkBodyId { get; set; }

    private IList<Chapter>? _chapters;
    public IList<Chapter> Chapters => _chapters ??= [];

    private IList<Book>? _books;
    public IList<Book> Books => _books ??= [];

    private ICollection<Anthology>? _anthologies;
    public ICollection<Anthology> Anthologies => _anthologies ??= [];

    public bool HasContent => WorkBodyId?.Timestamp != default;
    public bool HasChapters => Chapters.Count > 0;
    public bool HasBooks => Books.Count > 0;
    public bool IsValid => !(HasContent || HasChapters || HasBooks) // New story
        || (HasContent && !(HasChapters || HasBooks)) // One shot
        || (HasChapters && !(HasContent || HasBooks)) // Normal story (serial)
        || (HasBooks && !(HasContent || HasChapters)); // Series
}
