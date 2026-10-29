using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using IHFiction.WebClient.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace IHFiction.UnitTests.WebClient;

public sealed class VisitorIdentityTests
{
    [Fact]
    public async Task ConcurrentVisitors_ReplaceSpoofedHeader_WithoutLeakingIdentityOrTokens()
    {
        var requests = new System.Collections.Concurrent.ConcurrentBag<(string? Ip, string? Token)>();
        async Task Send(string ip, string token)
        {
            var visitor = new VisitorContext { ClientIp = ip };
            using var client = new HttpClient(new VisitorIdentityHandler(visitor, NullLogger<VisitorIdentityHandler>.Instance)
            {
                InnerHandler = new CaptureHandler(request => requests.Add((request.Headers.GetValues("X-Forwarded-For").Single(), request.Headers.Authorization?.Parameter)))
            });
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://fiction/stories");
            request.Headers.Add("X-Forwarded-For", "192.0.2.99, 192.0.2.98");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        }
        await Task.WhenAll(Send("::ffff:203.0.113.1", "first"), Send("203.0.113.2", "second"));
        requests.Should().BeEquivalentTo(new[] { (Ip: (string?)"203.0.113.1", Token: (string?)"first"), (Ip: (string?)"203.0.113.2", Token: (string?)"second") });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public async Task MissingContext_RemovesUntrustedHeader(string? ip)
    {
        using var client = new HttpClient(new VisitorIdentityHandler(new VisitorContext { ClientIp = ip }, NullLogger<VisitorIdentityHandler>.Instance)
        {
            InnerHandler = new CaptureHandler(request => request.Headers.Contains("X-Forwarded-For").Should().BeFalse())
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://fiction/stories");
        request.Headers.Add("X-Forwarded-For", "192.0.2.99");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed class CaptureHandler(Action<HttpRequestMessage> capture) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            capture(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
