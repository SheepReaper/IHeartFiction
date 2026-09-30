using FluentAssertions;

using IHFiction.SharedKernel.AgentSkills;

namespace IHFiction.UnitTests.WebClient;

public sealed class AgentSkillsEndpointTests
{
    [Fact]
    public void ReadContentSkill_Matches_CatalogDefinition()
    {
        AgentSkillsCatalog.ReadContentSkillName.Should().Be("read-iheartfiction-content");
        AgentSkillsCatalog.ReadContentSkillType.Should().Be("skill-md");
        AgentSkillsCatalog.ReadContentSkillRelativeUrl.Should().Be("/.well-known/agent-skills/read-iheartfiction-content/SKILL.md");

        var doc = AgentSkillsCatalog.CreateDiscoveryDocument();
        doc.Skills.Should().ContainSingle();
        doc.Skills[0].Name.Should().Be("read-iheartfiction-content");
        doc.Skills[0].Type.Should().Be("skill-md");
        doc.Skills[0].Url.Should().Be("/.well-known/agent-skills/read-iheartfiction-content/SKILL.md");
        doc.Skills[0].Digest.Should().Be(AgentSkillsCatalog.ReadContentSkillDigest);
    }
}
