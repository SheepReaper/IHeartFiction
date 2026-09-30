using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

using FluentAssertions;

using IHFiction.SharedKernel.AgentSkills;

namespace IHFiction.IntegrationTests.Stories;

public sealed class AgentSkillsIntegrationTests(IntegrationTestWebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetAgentSkillsIndex_Returns200WithSchema020AndValidDigest()
    {
        var response = await _client.GetAsync(
            "/.well-known/agent-skills/index.json",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("$schema").GetString()
            .Should().Be(AgentSkillsCatalog.DiscoverySchemaUri);

        var skills = root.GetProperty("skills");
        skills.GetArrayLength().Should().Be(1);

        var skill = skills[0];
        skill.GetProperty("name").GetString().Should().Be(AgentSkillsCatalog.ReadContentSkillName);
        skill.GetProperty("type").GetString().Should().Be("skill-md");
        skill.GetProperty("description").GetString().Should().Be(AgentSkillsCatalog.ReadContentSkillDescription);
        skill.GetProperty("url").GetString().Should().Be(AgentSkillsCatalog.ReadContentSkillRelativeUrl);
        skill.GetProperty("digest").GetString().Should().Be(AgentSkillsCatalog.ReadContentSkillDigest);
    }

    [Fact]
    public async Task HeadAgentSkillsIndex_Returns200()
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, "/.well-known/agent-skills/index.json");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSkillMarkdown_Returns200AndByteForByteMatchesIndexDigest()
    {
        var response = await _client.GetAsync(
            AgentSkillsCatalog.ReadContentSkillRelativeUrl,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/markdown");

        var servedBytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var computedDigest = $"sha256:{Convert.ToHexStringLower(SHA256.HashData(servedBytes))}";

        computedDigest.Should().Be(AgentSkillsCatalog.ReadContentSkillDigest);

        var markdown = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        markdown.Should().StartWith("---\n");
        markdown.Should().Contain(AgentSkillsCatalog.ReadContentSkillName);
    }

    [Fact]
    public async Task HeadSkillMarkdown_Returns200()
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, AgentSkillsCatalog.ReadContentSkillRelativeUrl);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUnknownSkillMarkdown_Returns404NotFound()
    {
        var response = await _client.GetAsync(
            "/.well-known/agent-skills/non-existent-skill/SKILL.md",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
