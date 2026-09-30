namespace IHFiction.SharedKernel.AiCatalog;

public static class AiCatalog
{
    public const string SpecVersion = "1.0";
    public const string HostDisplayName = "I❤️Fiction";
    public const string HostIdentifier = "did:web:iheartfiction.net";

    public static AiCatalogDocument CreateDocument(Uri? siteBaseUri = null, Uri? apiBaseUri = null)
    {
        var site = siteBaseUri?.ToString().TrimEnd('/') ?? "https://iheartfiction.net";
        var api = apiBaseUri?.ToString().TrimEnd('/') ?? "https://api.iheartfiction.net";

        return new AiCatalogDocument(
            SpecVersion,
            new AiCatalogHost(HostDisplayName, HostIdentifier),
            [
                new(
                    "urn:air:iheartfiction.net:api:fiction-api",
                    "IHeartFiction API",
                    "application/vnd.oai.openapi+json",
                    $"{api}/openapi/v1.json",
                    "REST API for discovering, reading, and interacting with published fiction, chapters, and author profiles.",
                    [
                        "Find stories about space exploration",
                        "Read the latest chapters of published fiction",
                        "Search published stories by author or tag",
                    ],
                    ["fiction", "stories", "api", "openapi"]
                ),
                new(
                    "urn:air:iheartfiction.net:skill:read-iheartfiction-content",
                    "Read IHeartFiction Content Skill",
                    "application/ai-skill+md",
                    $"{site}/.well-known/agent-skills/read-iheartfiction-content/SKILL.md",
                    "Agent skill for discovering, searching, and reading published fiction stories and chapters using public HTTP/REST and markdown interfaces.",
                    [
                        "How do I search and read stories on IHeartFiction?",
                        "Fetch markdown content for a story on IHeartFiction",
                        "Get instructions for reading published fiction",
                    ],
                    ["skill", "agent-skills", "markdown", "fiction"]
                ),
                new(
                    "urn:air:iheartfiction.net:discovery:agent-skills-index",
                    "Agent Skills Discovery Index",
                    "application/vnd.agentskills.v0.2.0+json",
                    $"{site}/.well-known/agent-skills/index.json",
                    "RFC 0.2.0 compliant discovery catalog listing verified Agent Skills published by IHeartFiction.",
                    [
                        "Discover available agent skills on IHeartFiction",
                        "List all supported AI agent capabilities",
                    ],
                    ["discovery", "agent-skills", "index"]
                ),
                new(
                    "urn:air:iheartfiction.net:auth:auth-md",
                    "IHeartFiction Auth.md",
                    "text/markdown",
                    $"{site}/auth.md",
                    "Agent authentication and verified ID-JAG registration documentation.",
                    [
                        "How do agents authenticate with IHeartFiction?",
                        "Register an autonomous agent identity on IHeartFiction",
                    ],
                    ["auth", "security", "id-jag", "agent-auth"]
                ),
            ]);
    }
}
