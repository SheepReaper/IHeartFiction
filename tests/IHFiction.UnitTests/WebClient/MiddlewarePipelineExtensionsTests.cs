using System.Text.Json;

using FluentAssertions;

using IHFiction.WebClient.Extensions;

using Microsoft.Extensions.Configuration;

namespace IHFiction.UnitTests.WebClient;

public sealed class MiddlewarePipelineExtensionsTests
{
    [Theory]
    [InlineData(
        "/.well-known/openid-configuration",
        "https://auth.example.test/realms/fiction/.well-known/openid-configuration")]
    [InlineData(
        "/.well-known/oauth-authorization-server",
        "https://auth.example.test/realms/fiction/.well-known/oauth-authorization-server")]
    public void BuildDiscoveryMetadataUri_AppendsPathToConfiguredAuthority(string path, string expected)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OidcAuthority"] = "https://auth.example.test/realms/fiction/",
            })
            .Build();

        MiddlewarePipelineExtensions.BuildDiscoveryMetadataUri(configuration, path)
            .Should().Be(expected);
    }

    [Fact]
    public void BuildDiscoveryMetadataUri_WithoutAuthority_Throws()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        var action = () => MiddlewarePipelineExtensions.BuildDiscoveryMetadataUri(
            configuration,
            "/.well-known/openid-configuration");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("OidcAuthority configuration is required for identity discovery.");
    }

    [Fact]
    public void CreateProtectedResourceMetadata_UsesSiteResourceAndKeycloakIssuer()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OidcAuthority"] = "https://auth.example.test/realms/fiction/",
            })
            .Build();

        var metadata = MiddlewarePipelineExtensions.CreateProtectedResourceMetadata(
            new Uri("https://www.example.test/"),
            configuration);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(metadata));

        json.RootElement.GetProperty("resource").GetString()
            .Should().Be("https://www.example.test");
        json.RootElement.GetProperty("authorization_servers").EnumerateArray()
            .Select(value => value.GetString())
            .Should().Equal("https://auth.example.test/realms/fiction");
        json.RootElement.GetProperty("scopes_supported").EnumerateArray()
            .Select(value => value.GetString())
            .Should().Equal("openid", "profile", "fiction_api");
        json.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().Equal("resource", "authorization_servers", "scopes_supported");
    }

    [Fact]
    public void CreateAuthMd_DocumentsApiAgentFlowFromCanonicalSiteEndpoint()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OidcAuthority"] = "https://auth.example.test/realms/fiction",
            })
            .Build();

        var markdown = MiddlewarePipelineExtensions.CreateAuthMd(
            new Uri("https://api.example.test"),
            configuration);

        markdown.Should().StartWith("# auth.md");
        markdown.Should().Contain("https://api.example.test/.well-known/oauth-protected-resource");
        markdown.Should().Contain("POST https://api.example.test/oauth2/token");
        markdown.Should().Contain("https://auth.example.test/realms/fiction");
    }
}
