using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Csp;
using IHFiction.SharedWeb.Features.Reading;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace IHFiction.UnitTests.Csp;

public sealed class CspPolicyProviderTests
{
    [Theory]
    [InlineData("https://auth.example.test/realms/fiction", "https://auth.example.test")]
    [InlineData("https://localhost:8080/realms/fiction", "https://localhost:8080")]
    public void AuthenticatedDocumentPolicy_AllowsFormRedirectOnlyToConfiguredIdentityProvider(string authority, string origin)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["OidcAuthority"] = authority }).Build());
        services.AddSingleton(Options.Create(new SiteUrlOptions { BaseUrl = new Uri("https://example.test") }));
        services.AddSingleton(Options.Create(new DisqusOptions { ShortName = "example" }));
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddCspProvider();
        using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<CspPolicyProvider>();
        var policy = policyProvider.RenderAuthenticatedDocumentPolicy("nonce");
        var formAction = policy.Split(';').Single(d => d.TrimStart().StartsWith("form-action ", StringComparison.Ordinal)).Trim();
        Assert.Equal($"form-action 'self' {origin}", formAction);
        Assert.Contains("form-action 'self';", policyProvider.RenderPolicy("nonce"));
        Assert.Contains("form-action 'self';", policyProvider.RenderRemoteSignOutPolicy("nonce"));
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void RenderPolicy_AllowsBrowserRefreshOnlyInDevelopment(string environmentName, bool allowsRefresh)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        var provider = new CspPolicyProvider(
            new SiteUrlOptions { BaseUrl = new Uri("https://localhost:7000") },
            new DisqusOptions { ShortName = "example" }, environment, new Uri("https://auth.example.test/realms/fiction"));

        var policy = provider.RenderPolicy("test-nonce");
        var connectSources = policy.Split(';').Single(directive => directive.TrimStart().StartsWith("connect-src ", StringComparison.Ordinal));

        Assert.Equal(allowsRefresh, connectSources.Contains(" ws://localhost:*", StringComparison.Ordinal));
        Assert.Equal(allowsRefresh, connectSources.Contains(" wss://localhost:*", StringComparison.Ordinal));
        Assert.Contains("'self' wss://localhost", connectSources);
        Assert.Contains("https://links.services.disqus.com", connectSources);
        Assert.Contains("'nonce-test-nonce'", policy);
    }
}
