using System.Text.Json.Serialization;

namespace IHFiction.SharedKernel.ApiCatalog;

public sealed record LinkTarget(
    [property: JsonPropertyName("href")]
    string Href,

    [property: JsonPropertyName("type")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Type = null,

    [property: JsonPropertyName("title")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Title = null
);
