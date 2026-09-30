using System.Text.Json.Serialization;

namespace IHFiction.SharedKernel.AiCatalog;

public sealed record AiCatalogHost(
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("identifier")] string Identifier);
