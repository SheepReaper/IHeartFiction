using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using FluentAssertions;

using IHFiction.SharedKernel.AgentSkills;

namespace IHFiction.UnitTests.Features.AgentAccess;

public sealed class AgentSkillsCatalogTests
{
    private static readonly Regex SkillNameRegex = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    [Fact]
    public void AgentSkillsDiscoveryDocument_Serializes_ConformingToSchema020()
    {
        var doc = AgentSkillsCatalog.CreateDiscoveryDocument();
        var json = JsonSerializer.Serialize(doc);
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        root.GetProperty("$schema").GetString()
            .Should().Be("https://schemas.agentskills.io/discovery/0.2.0/schema.json");

        var skills = root.GetProperty("skills");
        skills.GetArrayLength().Should().Be(1);

        var first = skills[0];
        first.GetProperty("name").GetString().Should().Be("read-iheartfiction-content");
        first.GetProperty("type").GetString().Should().Be("skill-md");
        first.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        first.GetProperty("url").GetString().Should().Be("/.well-known/agent-skills/read-iheartfiction-content/SKILL.md");
        first.GetProperty("digest").GetString().Should().StartWith("sha256:");
    }

    [Fact]
    public void AgentSkillsDiscoveryDocument_WithCustomBaseUrl_GeneratesAbsoluteUrl()
    {
        var doc = AgentSkillsCatalog.CreateDiscoveryDocument("https://iheartfiction.net");
        doc.Skills[0].Url.Should().Be("https://iheartfiction.net/.well-known/agent-skills/read-iheartfiction-content/SKILL.md");
    }

    [Fact]
    public void ReadContentSkillDigest_Matches_RecomputedSha256OfServedMarkdownBytes()
    {
        var markdownBytes = Encoding.UTF8.GetBytes(AgentSkillsCatalog.ReadContentSkillMarkdown);
        var expectedHash = $"sha256:{Convert.ToHexStringLower(SHA256.HashData(markdownBytes))}";

        AgentSkillsCatalog.ReadContentSkillDigest.Should().Be(expectedHash);
        AgentSkillsCatalog.ReadContentSkillDigest.Should().MatchRegex(@"^sha256:[0-9a-f]{64}$");
    }

    [Fact]
    public void ReadContentSkillName_ConformsToAgentSkillsNamingSpecification()
    {
        var name = AgentSkillsCatalog.ReadContentSkillName;

        name.Length.Should().BeInRange(1, 64);
        SkillNameRegex.IsMatch(name).Should().BeTrue("skill name must only contain lowercase alphanumeric and non-consecutive hyphens without leading/trailing hyphens");
    }

    [Fact]
    public void ReadContentSkillMarkdown_ContainsFrontmatterAndVerifiedReadingSections()
    {
        var markdown = AgentSkillsCatalog.ReadContentSkillMarkdown;

        markdown.Should().StartWith("---\n");
        markdown.Should().Contain($"name: {AgentSkillsCatalog.ReadContentSkillName}");
        markdown.Should().Contain("description: ");

        // Discovery
        markdown.Should().Contain("/.well-known/agent-skills/index.json");
        markdown.Should().Contain("/.well-known/api-catalog");
        markdown.Should().Contain("/openapi/v1.json");
        markdown.Should().Contain("/scalar/v1");

        // Search & Listing
        markdown.Should().Contain("GET https://api.iheartfiction.net/stories?q=");
        markdown.Should().Contain("GET https://api.iheartfiction.net/tags");
        markdown.Should().Contain("`q`");
        markdown.Should().Contain("`TagKey`");
        markdown.Should().Contain("`AuthorId`");
        markdown.Should().Contain("`CompletionStatus`");
        markdown.Should().Contain("`Sort`");
        markdown.Should().Contain("`Page`");
        markdown.Should().Contain("`PageSize`");
        markdown.Should().Contain("`Fields`");
        markdown.Should().Contain("\"data\":");
        markdown.Should().Contain("\"currentPage\":");
        markdown.Should().NotContain("/stories/published");

        // Story Details, Chapters & Work Metadata
        markdown.Should().Contain("/stories/{id}");
        markdown.Should().Contain("/stories/{id}/chapters");
        markdown.Should().Contain("/works/{id}/meta");

        // Content Retrieval
        markdown.Should().Contain("/chapters/{id}/content");
        markdown.Should().Contain("/stories/{id}/content");
        markdown.Should().Contain("/works/{id}/content");
        markdown.Should().Contain("Accept: text/markdown");

        // Authors
        markdown.Should().Contain("GET https://api.iheartfiction.net/authors?");
        markdown.Should().Contain("/authors/{id}");
        markdown.Should().Contain("GET https://api.iheartfiction.net/stories?AuthorId=");
        markdown.Should().NotContain("https://api.iheartfiction.net/authors/01J8Y6M1A2B3C4D5E6F7G8H9J0/stories");
        markdown.Should().NotContain("GET /authors/{id}/stories");

        // Auth Boundary & Read-Only defaults
        markdown.Should().Contain("Anonymous Public Access");
        markdown.Should().Contain("Optional Delegated Agent Authentication");
        markdown.Should().Contain("Strict Read-Only Enforcement");
        markdown.Should().Contain("agent.read");
        markdown.Should().Contain("profile.read");

        // Error handling
        markdown.Should().Contain("400 Bad Request");
        markdown.Should().Contain("404 Not Found");
        markdown.Should().Contain("429 Too Many Requests");
    }

    [Fact]
    public void RepositorySkillFile_Matches_AgentSkillsCatalogReadContentSkillMarkdown()
    {
        var repoRoot = FindRepositoryRoot();
        var skillPath = Path.Combine(repoRoot, ".agents", "skills", "read-iheartfiction-content", "SKILL.md");

        File.Exists(skillPath).Should().BeTrue($"expected repository skill file at {skillPath}");

        var fileMarkdown = File.ReadAllText(skillPath, Encoding.UTF8).ReplaceLineEndings("\n").TrimEnd('\n');
        var catalogMarkdown = AgentSkillsCatalog.ReadContentSkillMarkdown.ReplaceLineEndings("\n").TrimEnd('\n');

        fileMarkdown.Should().Be(catalogMarkdown);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "IHFiction.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root containing IHFiction.slnx.");
    }
}
