using System.Text.Json.Serialization;

namespace IHFiction.SharedKernel.AiCatalog;

public sealed record AiCatalogDocument(
    [property: JsonPropertyName("specVersion")] string SpecVersion,
    [property: JsonPropertyName("host")] AiCatalogHost Host,
    [property: JsonPropertyName("entries")] IReadOnlyList<AiCatalogEntry> Entries);
