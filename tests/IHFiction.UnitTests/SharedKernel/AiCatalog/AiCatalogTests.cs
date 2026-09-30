using System.Text.Json;

using FluentAssertions;

using IHFiction.SharedKernel.AiCatalog;

namespace IHFiction.UnitTests.SharedKernel.AiCatalog;

public sealed class AiCatalogTests
{
    [Fact]
    public void AiCatalogDocument_Serializes_ConformingToArd10()
    {
        var doc = IHFiction.SharedKernel.AiCatalog.AiCatalog.CreateDocument();
        var json = JsonSerializer.Serialize(doc);
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;

        root.GetProperty("specVersion").GetString().Should().Be("1.0");

        var host = root.GetProperty("host");
        host.GetProperty("displayName").GetString().Should().Be("I❤️Fiction");
        host.GetProperty("identifier").GetString().Should().Be("did:web:iheartfiction.net");

        var entries = root.GetProperty("entries");
        entries.GetArrayLength().Should().Be(4);

        var first = entries[0];
        first.GetProperty("identifier").GetString().Should().Be("urn:air:iheartfiction.net:api:fiction-api");
        first.GetProperty("displayName").GetString().Should().Be("IHeartFiction API");
        first.GetProperty("type").GetString().Should().Be("application/vnd.oai.openapi+json");
        first.GetProperty("url").GetString().Should().Be("https://api.iheartfiction.net/openapi/v1.json");
        first.GetProperty("description").GetString().Should().NotBeNullOrWhiteSpace();
        first.GetProperty("representativeQueries").GetArrayLength().Should().BeGreaterOrEqualTo(2);
        first.GetProperty("tags").GetArrayLength().Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void AiCatalogDocument_WithCustomBaseUrls_ConfiguresUrlsCorrectly()
    {
        var doc = IHFiction.SharedKernel.AiCatalog.AiCatalog.CreateDocument(
            new Uri("https://custom.site.test/"),
            new Uri("https://custom.api.test/"));

        doc.Entries.Should().ContainSingle(e => e.Identifier == "urn:air:iheartfiction.net:api:fiction-api")
            .Which.Url.Should().Be("https://custom.api.test/openapi/v1.json");

        doc.Entries.Should().ContainSingle(e => e.Identifier == "urn:air:iheartfiction.net:skill:read-iheartfiction-content")
            .Which.Url.Should().Be("https://custom.site.test/.well-known/agent-skills/read-iheartfiction-content/SKILL.md");

        doc.Entries.Should().ContainSingle(e => e.Identifier == "urn:air:iheartfiction.net:discovery:agent-skills-index")
            .Which.Url.Should().Be("https://custom.site.test/.well-known/agent-skills/index.json");

        doc.Entries.Should().ContainSingle(e => e.Identifier == "urn:air:iheartfiction.net:auth:auth-md")
            .Which.Url.Should().Be("https://custom.site.test/auth.md");
    }
}
