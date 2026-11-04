using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Csp;
using IHFiction.SharedWeb.Features.Reading;

using Microsoft.Extensions.Hosting;

using NSubstitute;

namespace IHFiction.UnitTests.Csp;

public sealed class CspPolicyProviderTests
{
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
            new DisqusOptions { ShortName = "example" }, environment);

        var policy = provider.RenderPolicy("test-nonce");
        var connectSources = policy.Split(';').Single(directive => directive.TrimStart().StartsWith("connect-src ", StringComparison.Ordinal));

        Assert.Equal(allowsRefresh, connectSources.Contains(" ws://localhost:*", StringComparison.Ordinal));
        Assert.Equal(allowsRefresh, connectSources.Contains(" wss://localhost:*", StringComparison.Ordinal));
        Assert.Contains("'self' wss://localhost", connectSources);
        Assert.Contains("https://links.services.disqus.com", connectSources);
        Assert.Contains("'nonce-test-nonce'", policy);
    }
}
