using System.Text.Json;

using FluentAssertions;

using IHFiction.SharedKernel.AgentAuth;

namespace IHFiction.UnitTests.SharedKernel.AgentAuth;

public sealed class AgentAuthTests
{
    [Fact]
    public void OAuthProtectedResourceMetadata_Serializes_Rfc9728CompliantJson()
    {
        OAuthProtectedResourceMetadata metadata = new(
            Resource: "https://api.iheartfiction.net",
            AuthorizationServers: ["https://api.iheartfiction.net", "https://auth.iheartfiction.net/realms/fiction"],
            ScopesSupported: ["agent.read", "profile.read"],
            BearerMethodsSupported: ["header"],
            ResourceDocumentation: "https://api.iheartfiction.net/auth.md");

        var json = JsonSerializer.Serialize(metadata);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("resource").GetString().Should().Be("https://api.iheartfiction.net");
        root.GetProperty("authorization_servers").EnumerateArray().First().GetString()
            .Should().Be("https://api.iheartfiction.net");
        root.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()).Should().Contain("agent.read");
        root.GetProperty("bearer_methods_supported").EnumerateArray().Select(e => e.GetString()).Should().Contain("header");
        root.GetProperty("resource_documentation").GetString().Should().Be("https://api.iheartfiction.net/auth.md");
    }

    [Fact]
    public void AuthMdContent_Generate_DocumentsCompleteIdentityAssertionFlow()
    {
        var markdown = AuthMdContent.Generate(
            "https://api.example.com",
            "https://agents.example.com",
            "https://auth.example.com/realms/fiction");

        markdown.Should().Contain("# auth.md");
        markdown.Should().Contain("https://agents.example.com/.well-known/oauth-authorization-server");
        markdown.Should().Contain("https://auth.example.com/realms/fiction");
        markdown.Should().Contain("Authorization: Bearer <access-token>");
        markdown.Should().Contain("POST https://agents.example.com/agent/identity");
        markdown.Should().Contain("urn:ietf:params:oauth:token-type:id-jag");
        markdown.Should().Contain("interaction_required");
    }
}
