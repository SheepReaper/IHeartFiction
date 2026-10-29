using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using FluentAssertions;
using IHFiction.WebClient.Extensions;
using IHFiction.WebClient.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace IHFiction.UnitTests.WebClient;

public sealed class FictionApiTransportTests
{
    [Fact]
    public async Task CallerScopes_KeepIdentityAndTokensSeparate_AndDoNotDisposePooledTransport()
    {
        var observations = new ConcurrentBag<(string Ip, string? Token)>();
        var pooled = new RecordingHandler(request => observations.Add((
            request.Headers.GetValues("X-Forwarded-For").Single(), request.Headers.Authorization?.Parameter)));
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddHttpContextAccessor();
        var auth = Substitute.For<IAuthenticationService>();
        auth.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string?>()).Returns(call =>
        {
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = (string)call.Arg<HttpContext>().Items["token"]! }]);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("test")), properties, "test"));
        });
        builder.Services.AddSingleton(auth);
        builder.AddApplicationServices();
        builder.Services.AddHttpClient(FictionApiTransport.Name).ConfigurePrimaryHttpMessageHandler(() => pooled);
        await using var provider = builder.Services.BuildServiceProvider();

        async Task Send(string ip, string token)
        {
            await using var scope = provider.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<VisitorContext>().ClientIp = ip;
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Items["token"] = token;
            scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
            var transport = scope.ServiceProvider.GetRequiredService<FictionApiTransport>();
            using var response = await transport.Client.GetAsync("/check", TestContext.Current.CancellationToken);
        }

        await Task.WhenAll(Send("203.0.113.1", "first"), Send("203.0.113.2", "second"));
        // Both caller scopes are now disposed; the cached inner transport must still work.
        await Send("203.0.113.3", "third");
        pooled.Disposed.Should().BeFalse();
        observations.Should().BeEquivalentTo(new[] { ("203.0.113.1", (string?)"first"), ("203.0.113.2", (string?)"second"), ("203.0.113.3", (string?)"third") });
    }

    private sealed class RecordingHandler(Action<HttpRequestMessage> record) : HttpMessageHandler
    {
        public bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            record(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
