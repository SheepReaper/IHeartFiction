namespace IHFiction.Data.Stories.Domain;

public sealed class Book : Work
{
    public required string Description { get; set; }

    private IList<Chapter>? _chapters;
    public IList<Chapter> Chapters => _chapters ??= [];

    public Story? Story { get; set; }
    public Ulid? StoryId { get; set; }
}
