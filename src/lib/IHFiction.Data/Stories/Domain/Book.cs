namespace IHFiction.Data.Stories.Domain;

public sealed class Book : Work
{
    public string Description { get; set; } = default!;

    private IList<Chapter>? _chapters;
    public IList<Chapter> Chapters => _chapters ??= [];

    public int Order { get; set; }

    public Story Story { get; set; } = default!;
    public Ulid StoryId { get; set; }

    public bool HasContent => Chapters.Count == 0 && PublishedAt != null;
}
