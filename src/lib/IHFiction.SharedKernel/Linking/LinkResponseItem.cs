using System.ComponentModel.DataAnnotations;

namespace IHFiction.SharedKernel.Linking;

/// <summary>
/// An object representing a hypermedia link.
/// </summary>
/// <param name="Href">The URL of the linked resource.</param>
/// <param name="Rel">The relationship type of the link.</param>
/// <param name="Method">The HTTP method used to access the linked resource.</param>
public sealed record LinkItem(
    [property: DataType(DataType.Url)]
    string Href,
    string Rel,
    string Method
);
