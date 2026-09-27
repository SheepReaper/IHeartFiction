using System.Text.Json.Serialization;

namespace IHFiction.SharedKernel.ApiCatalog;

public sealed record LinksetDocument(
    [property: JsonPropertyName("linkset")]
    IReadOnlyList<LinkContext> Linkset
);
