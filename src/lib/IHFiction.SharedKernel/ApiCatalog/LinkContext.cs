using System.Text.Json.Serialization;

namespace IHFiction.SharedKernel.ApiCatalog;

public sealed record LinkContext(
    [property: JsonPropertyName("anchor")]
    string Anchor,

    [property: JsonPropertyName("item")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<LinkTarget>? Items = null,

    [property: JsonPropertyName("service-desc")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<LinkTarget>? ServiceDescription = null,

    [property: JsonPropertyName("service-doc")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<LinkTarget>? ServiceDocumentation = null
);
