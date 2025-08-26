namespace IHFiction.Data.Stories.Domain;

public sealed class Anthology : Work
{
    public required string Description { get; set; }

    private IList<Story>? _stories;
    public IList<Story> Stories => _stories ??= [];
}
