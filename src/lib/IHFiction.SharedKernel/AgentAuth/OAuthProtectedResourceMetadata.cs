using System.Text.Json.Serialization;

namespace IHFiction.SharedKernel.AgentAuth;

public sealed record OAuthProtectedResourceMetadata(
    [property: JsonPropertyName("resource")]
    string Resource,
    [property: JsonPropertyName("authorization_servers")]
    IReadOnlyList<string> AuthorizationServers,
    [property: JsonPropertyName("scopes_supported")]
    IReadOnlyList<string>? ScopesSupported = null,
    [property: JsonPropertyName("bearer_methods_supported")]
    IReadOnlyList<string>? BearerMethodsSupported = null,
    [property: JsonPropertyName("resource_documentation")]
    string? ResourceDocumentation = null
);
