namespace IHFiction.SharedKernel.Linking;

/// <summary>
/// A contract for types that include hypermedia links.
/// </summary>
public interface ILinks
{
    /// <summary>
    /// A collection of hypermedia links
    /// </summary>
    public IEnumerable<LinkItem> Links { get; init; }
}
