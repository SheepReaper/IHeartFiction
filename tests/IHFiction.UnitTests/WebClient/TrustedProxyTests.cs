using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace IHFiction.UnitTests.WebClient;

public sealed class TrustedProxyTests
{
    [Fact]
    public async Task HostPipeline_UsesRegisteredOptionsOnce_AndRetainsHostFiltering()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration["ForwardedHeaders_Enabled"] = "false";
        builder.Configuration["TrustedProxies"] = "127.0.0.0/8";
        builder.Configuration["AllowedHosts"] = "fiction";
        builder.AddTrustedProxyForwarding();
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseTrustedProxyForwarding();
        app.UseTrustedProxyForwarding();
        app.MapGet("/identity", (HttpContext context) => Results.Text($"{context.Connection.RemoteIpAddress}|{context.Request.Scheme}|{context.Request.Host}"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/identity");
        request.Headers.Host = "fiction";
        request.Headers.Add("X-Forwarded-For", "192.0.2.99, 127.0.0.2");
        request.Headers.Add("X-Forwarded-Proto", "http, https");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("127.0.0.2|https|fiction");
        using var invalidHost = new HttpRequestMessage(HttpMethod.Get, "/identity");
        invalidHost.Headers.Host = "attacker.test";
        using var rejected = await client.SendAsync(invalidHost, TestContext.Current.CancellationToken);
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task AutomaticForwarding_IsRejectedWithoutOverwritingConfiguration(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration["ForwardedHeaders_Enabled"] = "true";
        builder.Configuration["TrustedProxies"] = "127.0.0.1/32";
        builder.AddTrustedProxyForwarding();
        builder.Configuration["ForwardedHeaders_Enabled"].Should().Be("true");
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseTrustedProxyForwarding();
        var start = () => app.StartAsync(TestContext.Current.CancellationToken);
        await start.Should().ThrowAsync<OptionsValidationException>();
    }

    [Theory]
    [InlineData("10.0.9.39", "203.0.113.8", "https")]
    [InlineData("198.51.100.9", "198.51.100.9", "http")]
    public async Task Forwarding_OnlyAcceptsOneHopFromTrustedPeer(string peer, string expectedIp, string expectedScheme)
    {
        var options = new ForwardedHeadersOptions();
        TrustedProxyExtensions.ConfigureTrustedProxyOptions(options, Configuration("10.0.9.0/24"));
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("fiction");
        context.Request.Headers["X-Forwarded-For"] = "192.0.2.99, 203.0.113.8";
        context.Request.Headers["X-Forwarded-Proto"] = "http, https";
        context.Request.Headers["X-Forwarded-Host"] = "attacker.test";
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
        await middleware.Invoke(context);
        context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse(expectedIp));
        context.Request.Scheme.Should().Be(expectedScheme);
        context.Request.Host.Value.Should().Be("fiction");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("10.0.9.0/24,invalid")]
    [InlineData("0.0.0.0/0")]
    public void InvalidTrustConfiguration_Fails(string? cidrs)
    {
        var action = () => TrustedProxyExtensions.ConfigureTrustedProxyOptions(new ForwardedHeadersOptions(), Configuration(cidrs));
        action.Should().Throw<InvalidOperationException>();
    }

    private static IConfiguration Configuration(string? cidrs) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["TrustedProxies"] = cidrs }).Build();
}
