namespace IHFiction.SharedKernel.Linking;

/// <summary>
/// A Response model decorated with a collection of hyperlinks under the <paramref name="Links"/> property.
/// </summary>
/// <typeparam name="T">The type of the wrapped value.</typeparam>
/// <param name="Value">The value to wrap.</param>
/// <param name="Links">The collection of links associated with <paramref name="Value"/>.</param>
public record Linked<T>(T Value, IEnumerable<LinkItem> Links) : ILinks;
