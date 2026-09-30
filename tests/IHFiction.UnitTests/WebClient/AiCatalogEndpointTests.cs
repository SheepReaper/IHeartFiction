using FluentAssertions;

using IHFiction.SharedKernel.AiCatalog;

namespace IHFiction.UnitTests.WebClient;

public sealed class AiCatalogEndpointTests
{
    [Fact]
    public void AiCatalog_Contains_CoreCapabilities()
    {
        var doc = AiCatalog.CreateDocument();

        doc.SpecVersion.Should().Be("1.0");
        doc.Host.DisplayName.Should().Be("I❤️Fiction");
        doc.Entries.Should().HaveCount(4);

        doc.Entries.Should().Contain(e => e.Identifier == "urn:air:iheartfiction.net:api:fiction-api");
        doc.Entries.Should().Contain(e => e.Identifier == "urn:air:iheartfiction.net:skill:read-iheartfiction-content");
        doc.Entries.Should().Contain(e => e.Identifier == "urn:air:iheartfiction.net:discovery:agent-skills-index");
        doc.Entries.Should().Contain(e => e.Identifier == "urn:air:iheartfiction.net:auth:auth-md");
    }
}
