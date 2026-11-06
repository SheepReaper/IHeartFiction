using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace IHFiction.SharedKernel.AiCatalog;

public sealed record DidWebDocument(
    [property: JsonPropertyName("@context")] IReadOnlyList<string> Context,
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("alsoKnownAs")] IReadOnlyList<string> AlsoKnownAs,
    [property: JsonPropertyName("service")] IReadOnlyList<DidWebService> Service);

[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON serialization model for W3C did:web specification")]
[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "JSON serialization model for W3C did:web specification")]
public sealed record DidWebService(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("serviceEndpoint")] string ServiceEndpoint);
