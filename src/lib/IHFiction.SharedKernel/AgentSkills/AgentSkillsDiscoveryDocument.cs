using System.Text.Json.Serialization;

namespace IHFiction.SharedKernel.AgentSkills;

public sealed record AgentSkillsDiscoveryDocument(
    [property: JsonPropertyName("$schema")] string Schema,
    [property: JsonPropertyName("skills")] IReadOnlyList<AgentSkillEntry> Skills);
