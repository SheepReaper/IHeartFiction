using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace IHFiction.SharedKernel.AgentSkills;

[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON serialization model for RFC 0.2.0 agent skills discovery")]
[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "JSON serialization model for RFC 0.2.0 agent skills discovery")]
public sealed record AgentSkillEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("digest")] string Digest);
