namespace IHFiction.SharedKernel.Linking;

public interface ILinks
{
    /// <summary>
    /// A collection of hypermedia links
    /// </summary>
    public IEnumerable<LinkItem> Links { get; init; }
}
