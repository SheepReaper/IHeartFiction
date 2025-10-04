namespace IHFiction.Data.Stories.Domain;

public sealed class Anthology : Work
{
    public string Description { get; set; } = default!;

    private IList<Story>? _stories;
    public IList<Story> Stories => _stories ??= [];
}
