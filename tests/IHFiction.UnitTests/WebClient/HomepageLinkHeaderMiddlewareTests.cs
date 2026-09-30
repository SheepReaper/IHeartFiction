using FluentAssertions;

using IHFiction.SharedWeb.Configuration;
using IHFiction.WebClient.AgentDiscovery;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace IHFiction.UnitTests.WebClient;

public sealed class HomepageLinkHeaderMiddlewareTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task InvokeAsync_ForSuccessfulHomepage_AddsDiscoveryLinks(string method)
    {
        var context = CreateContext(method, "/");
        var sut = CreateMiddleware(StatusCodes.Status200OK);

        await sut.InvokeAsync(context, Options.Create(new ApiUrlOptions
        {
            BaseUrl = new Uri("https://api.example.test"),
        }));

        context.Response.Headers.Link.Should().BeEquivalentTo([
            "</.well-known/api-catalog>; rel=\"api-catalog\"; type=\"application/linkset+json\"",
            "</.well-known/ai-catalog.json>; rel=\"ai-catalog\"; type=\"application/json\"",
            "</.well-known/agent-skills/index.json>; rel=\"agent-skills\"; type=\"application/vnd.agentskills.v0.2.0+json\"",
            "<https://api.example.test/.well-known/oauth-protected-resource>; rel=\"oauth-protected-resource\"",
            "<https://api.example.test/openapi/v1.json>; rel=\"service-desc\"; type=\"application/vnd.oai.openapi+json\"",
            "<https://api.example.test/scalar/v1>; rel=\"service-doc\"; type=\"text/html\"",
            "<https://api.example.test/openapi/v1.json>; rel=\"describedby\"; type=\"application/vnd.oai.openapi+json\"",
            "</auth.md>; rel=\"describedby\"; type=\"text/markdown\"",
        ]);
    }

    [Theory]
    [InlineData("POST", "/", StatusCodes.Status200OK)]
    [InlineData("GET", "/stories", StatusCodes.Status200OK)]
    [InlineData("GET", "/", StatusCodes.Status500InternalServerError)]
    public async Task InvokeAsync_ForIneligibleResponse_DoesNotAddDiscoveryLinks(string method, string path, int statusCode)
    {
        var context = CreateContext(method, path);
        var sut = CreateMiddleware(statusCode);

        await sut.InvokeAsync(context, Options.Create(new ApiUrlOptions
        {
            BaseUrl = new Uri("https://api.example.test"),
        }));

        context.Response.Headers.Should().NotContainKey("Link");
    }

    private static HomepageLinkHeaderMiddleware CreateMiddleware(int statusCode) =>
        new(async context =>
        {
            context.Response.StatusCode = statusCode;
            if (context.Features.Get<IHttpResponseFeature>() is TestHttpResponseFeature feature)
            {
                await feature.FireOnStartingAsync();
            }

            await context.Response.WriteAsync("response");
        });

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        var responseFeature = new TestHttpResponseFeature();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }

    private sealed class TestHttpResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted { get; private set; }

        public void OnStarting(Func<object, Task> callback, object state) => _onStarting.Add((callback, state));
        public void OnCompleted(Func<object, Task> callback, object state) { }

        public async Task FireOnStartingAsync()
        {
            if (HasStarted) return;
            HasStarted = true;
            for (var i = _onStarting.Count - 1; i >= 0; i--)
            {
                var (callback, state) = _onStarting[i];
                await callback(state);
            }
        }
    }
}
