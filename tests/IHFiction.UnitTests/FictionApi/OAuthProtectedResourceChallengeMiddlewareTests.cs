using FluentAssertions;

using IHFiction.FictionApi.Infrastructure;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace IHFiction.UnitTests.FictionApi;

public sealed class OAuthProtectedResourceChallengeMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ForUnauthorizedResponse_AddsProtectedResourceMetadataChallenge()
    {
        var context = CreateContext();
        var sut = CreateMiddleware(StatusCodes.Status401Unauthorized);

        await sut.InvokeAsync(context, CreateOptions());

        context.Response.Headers.WWWAuthenticate.Should().ContainSingle()
            .Which.Should().Be(
                "Bearer resource_metadata=\"https://api.example.test/.well-known/oauth-protected-resource\"");
    }

    [Fact]
    public async Task InvokeAsync_ForUnauthorizedBearerChallenge_PreservesExistingParameters()
    {
        var context = CreateContext();
        context.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
        var sut = CreateMiddleware(StatusCodes.Status401Unauthorized);

        await sut.InvokeAsync(context, CreateOptions());

        context.Response.Headers.WWWAuthenticate.Should().ContainSingle()
            .Which.Should().Be(
                "Bearer error=\"invalid_token\", resource_metadata=\"https://api.example.test/.well-known/oauth-protected-resource\"");
    }

    [Fact]
    public async Task InvokeAsync_ForUnauthorizedResponse_PreservesOtherAuthenticationSchemes()
    {
        var context = CreateContext();
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"legacy\"";
        var sut = CreateMiddleware(StatusCodes.Status401Unauthorized);

        await sut.InvokeAsync(context, CreateOptions());

        context.Response.Headers.WWWAuthenticate.Should().BeEquivalentTo([
            "Basic realm=\"legacy\"",
            "Bearer resource_metadata=\"https://api.example.test/.well-known/oauth-protected-resource\"",
        ]);
    }

    [Theory]
    [InlineData(StatusCodes.Status200OK)]
    [InlineData(StatusCodes.Status403Forbidden)]
    public async Task InvokeAsync_ForNonUnauthorizedResponse_DoesNotAddChallenge(int statusCode)
    {
        var context = CreateContext();
        var sut = CreateMiddleware(statusCode);

        await sut.InvokeAsync(context, CreateOptions());

        context.Response.Headers.Should().NotContainKey(HeaderNames.WWWAuthenticate);
    }

    private static OAuthProtectedResourceChallengeMiddleware CreateMiddleware(int statusCode) =>
        new(async context =>
        {
            context.Response.StatusCode = statusCode;
            if (context.Features.Get<IHttpResponseFeature>() is TestHttpResponseFeature feature)
            {
                await feature.FireOnStartingAsync();
            }

            await context.Response.WriteAsync("response");
        });

    private static IOptions<BaseUrlOptions> CreateOptions() => Options.Create(new BaseUrlOptions
    {
        BaseUrl = new Uri("https://api.example.test"),
    });

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new TestHttpResponseFeature());
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
