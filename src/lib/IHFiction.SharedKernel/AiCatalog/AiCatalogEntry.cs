using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace IHFiction.SharedKernel.AiCatalog;

[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON serialization model for ARD 1.0 AI Catalog specification")]
[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "JSON serialization model for ARD 1.0 AI Catalog specification")]
public sealed record AiCatalogEntry(
    [property: JsonPropertyName("identifier")] string Identifier,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("representativeQueries")] IReadOnlyList<string> RepresentativeQueries,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags);
