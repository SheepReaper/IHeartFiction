namespace IHFiction.SharedKernel.Linking;

/// <summary>
/// Entity wrapper that provides a Links collection to an arbitrary object
/// </summary>
/// <param name="Value">The value to wrap</param>
/// <param name="Links">The collection of links</param>
/// <typeparam name="T">The type of the value to wrap</typeparam>
public record Linked<T>(T Value, IEnumerable<LinkItem> Links) : ILinks;
