using System.Net;
using System.Threading.RateLimiting;
using FluentAssertions;
using IHFiction.WebClient.Extensions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IHFiction.UnitTests.WebClient;

public sealed class WebRateLimitingTests
{
    [Theory]
    [InlineData("/stories", "general")]
    [InlineData("/not-a-route", "general")]
    [InlineData("/authentication/login", "authentication")]
    [InlineData("/AUTHENTICATION/LOGIN/", "authentication")]
    [InlineData("/authentication/logout", "authentication")]
    [InlineData("/authentication/refresh", "authentication")]
    [InlineData("/signin-oidc", "callbacks")]
    [InlineData("/signout-callback-oidc", "callbacks")]
    [InlineData("/_blazor/negotiate", "connections")]
    [InlineData("/_blazor/negotiate/", "connections")]
    [InlineData("/csp-reports", "reports")]
    [InlineData("/csp-reports/", "reports")]
    [InlineData("/_content/missing.js", "general")]
    [InlineData("/_blazor", null)]
    [InlineData("/health", null)]
    [InlineData("/alive", null)]
    [InlineData("/uptime", null)]
    public void Classification_SelectsOnePolicy(string path, string? policy)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        WebRateLimitingExtensions.ResolvePolicy(context, new OpenIdConnectOptions()).Should().Be(policy);
    }

    [Fact]
    public void StaticAsset_UsesEndpointMetadata_NotPathPrefix()
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new StaticAssetDescriptor { Route = "app.css", AssetPath = "app.css" }), "asset"));
        WebRateLimitingExtensions.ResolvePolicy(context, new OpenIdConnectOptions()).Should().BeNull();
    }

    [Fact]
    public async Task Limits_AreIndependentByVisitorAndPolicy_AndNormalizeMappedAddresses()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddWebRateLimiting(Configuration());
        await using var provider = services.BuildServiceProvider();
        var limiter = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter!;
        using var first = await limiter.AcquireAsync(Context("/stories", "203.0.113.1"), 1, TestContext.Current.CancellationToken);
        using var same = await limiter.AcquireAsync(Context("/stories", "::ffff:203.0.113.1"), 1, TestContext.Current.CancellationToken);
        using var different = await limiter.AcquireAsync(Context("/stories", "203.0.113.2"), 1, TestContext.Current.CancellationToken);
        using var auth = await limiter.AcquireAsync(Context("/authentication/login", "203.0.113.1"), 1, TestContext.Current.CancellationToken);
        first.IsAcquired.Should().BeTrue();
        same.IsAcquired.Should().BeFalse();
        different.IsAcquired.Should().BeTrue();
        auth.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task Rejections_SkipExpensiveWorkAndStatusPageReexecution()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["AllowedHosts"] = "127.0.0.1";
        builder.Services.AddWebRateLimiting(Configuration());
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseRouting();
        app.UseRateLimiter();
        app.UseStatusCodePagesWithReExecute("/not-found");
        var executions = 0;
        app.MapGet("/stories", () => { executions++; return Results.Text("rendered"); });
        app.MapGet("/not-found", () => Results.Text("wrong response"));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        using var allowed = await client.GetAsync("/stories", TestContext.Current.CancellationToken);
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        for (var i = 0; i < 2; i++)
        {
            using var rejected = await client.GetAsync("/stories", TestContext.Current.CancellationToken);
            rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            rejected.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero);
            rejected.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
            (await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().StartWith("Too many requests.");
        }
        executions.Should().Be(1);
    }

    [Fact]
    public async Task WebSocketUpgrades_UseConnectionBudget_WhilePollingRemainsAvailable()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["AllowedHosts"] = "127.0.0.1";
        builder.Services.AddWebRateLimiting(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["RateLimiting:Connections"] = "1" }).Build());
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseRouting();
        app.UseWebSockets();
        app.UseRateLimiter();
        app.MapGet("/_blazor", () => Results.Ok());
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        for (var i = 0; i < 2; i++)
        {
            using var upgrade = new HttpRequestMessage(HttpMethod.Get, "/_blazor");
            upgrade.Headers.Add("Connection", "Upgrade");
            upgrade.Headers.Add("Upgrade", "websocket");
            upgrade.Headers.Add("Sec-WebSocket-Version", "13");
            upgrade.Headers.Add("Sec-WebSocket-Key", Convert.ToBase64String(new byte[16]));
            using var response = await client.SendAsync(upgrade, TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(i == 0 ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests);
        }
        using var poll = await client.GetAsync("/_blazor?id=test", TestContext.Current.CancellationToken);
        poll.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void InvalidLimit_IsRejected()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddWebRateLimiting(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["RateLimiting:General"] = "0" }).Build());
        using var provider = services.BuildServiceProvider();
        var action = () => provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        action.Should().Throw<OptionsValidationException>();
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["RateLimiting:General"] = "1", ["RateLimiting:Authentication"] = "1" }).Build();

    private static HttpContext Context(string path, string ip)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return context;
    }
}
