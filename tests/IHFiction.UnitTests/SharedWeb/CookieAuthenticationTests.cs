using System.Security.Claims;
using System.Net;
using System.Text;

using FluentAssertions;

using IHFiction.SharedWeb.Extensions;
using IHFiction.SharedWeb.Infrastructure;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using NSubstitute;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class CookieAuthenticationTests
{
    [Fact]
    public void CreateLoginProperties_CreatesPersistentSession()
    {
        var context = new DefaultHttpContext();

        var properties = LoginLogoutEndpointRouteBuilderExtensions.CreateLoginProperties("/stories", context);

        properties.IsPersistent.Should().BeTrue();
        properties.RedirectUri.Should().Be("/stories");
    }

    [Fact]
    public void ConfigureCookieOidc_UsesExplicitFourteenDayTicketLifetime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureCookieOidc("cookie", "oidc");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("cookie");

        options.ExpireTimeSpan.Should().Be(TimeSpan.FromDays(14));
        options.Cookie.MaxAge.Should().Be(TimeSpan.FromDays(14));
        options.SlidingExpiration.Should().BeTrue();
    }

    [Fact]
    public void MergeRefreshedTokens_PreservesTokensOmittedByProvider()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([
            new AuthenticationToken { Name = OpenIdConnectParameterNames.AccessToken, Value = "old-access" },
            new AuthenticationToken { Name = OpenIdConnectParameterNames.IdToken, Value = "old-id" },
            new AuthenticationToken { Name = OpenIdConnectParameterNames.RefreshToken, Value = "old-refresh" },
            new AuthenticationToken { Name = OpenIdConnectParameterNames.TokenType, Value = "Bearer" },
            new AuthenticationToken { Name = "expires_at", Value = "2026-01-01T00:00:00.0000000+00:00" },
            new AuthenticationToken { Name = "custom-token", Value = "keep-me" },
        ]);

        var refreshed = new OpenIdConnectMessage
        {
            AccessToken = "new-access",
            ExpiresIn = "300",
        };

        var tokens = CookieOidcRefresher.MergeRefreshedTokens(
            properties,
            refreshed,
            new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero));

        tokens.Should().Contain(token => token.Name == OpenIdConnectParameterNames.AccessToken && token.Value == "new-access");
        tokens.Should().Contain(token => token.Name == OpenIdConnectParameterNames.IdToken && token.Value == "old-id");
        tokens.Should().Contain(token => token.Name == OpenIdConnectParameterNames.RefreshToken && token.Value == "old-refresh");
        tokens.Should().Contain(token => token.Name == OpenIdConnectParameterNames.TokenType && token.Value == "Bearer");
        tokens.Should().Contain(token => token.Name == "custom-token" && token.Value == "keep-me");
        tokens.Should().Contain(token => token.Name == "expires_at" && token.Value == "2026-09-27T12:05:00.0000000+00:00");
    }

    [Theory]
    [InlineData("invalid_grant", true)]
    [InlineData("invalid_request", false)]
    [InlineData(null, false)]
    public void IsInvalidSession_OnlyTreatsInvalidGrantAsRevoked(string? error, bool expected)
    {
        CookieOidcRefresher.IsInvalidSession(error).Should().Be(expected);
    }

    [Fact]
    public async Task RefreshPrincipalAsync_WithoutNewIdOrRefreshToken_PreservesSession()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "reader")], "cookie"));
        var properties = CreateProperties();
        var refresher = CreateRefresher(HttpStatusCode.OK, """
            {"access_token":"new-access","expires_in":300,"token_type":"Bearer"}
            """);

        var result = await refresher.RefreshPrincipalAsync(properties, principal, "oidc", TestContext.Current.CancellationToken);

        result.Status.Should().Be(CookieOidcRefresher.RefreshStatus.Success);
        result.Principal.Should().BeSameAs(principal);
        result.Tokens.Should().Contain(token => token.Name == OpenIdConnectParameterNames.IdToken && token.Value == "old-id");
        result.Tokens.Should().Contain(token => token.Name == OpenIdConnectParameterNames.RefreshToken && token.Value == "old-refresh");
    }

    [Fact]
    public async Task RefreshPrincipalAsync_WhenRefreshTokenIsRevoked_ReturnsInvalidSession()
    {
        var refresher = CreateRefresher(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");

        var result = await refresher.RefreshPrincipalAsync(
            CreateProperties(),
            new ClaimsPrincipal(),
            "oidc",
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(CookieOidcRefresher.RefreshStatus.InvalidSession);
    }

    [Fact]
    public async Task RefreshPrincipalAsync_WhenProviderIsUnavailable_RetainsSession()
    {
        var refresher = CreateRefresher(HttpStatusCode.ServiceUnavailable, """{"error":"temporarily_unavailable"}""");

        var result = await refresher.RefreshPrincipalAsync(
            CreateProperties(),
            new ClaimsPrincipal(),
            "oidc",
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(CookieOidcRefresher.RefreshStatus.TransientFailure);
    }

    private static AuthenticationProperties CreateProperties()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([
            new AuthenticationToken { Name = OpenIdConnectParameterNames.AccessToken, Value = "old-access" },
            new AuthenticationToken { Name = OpenIdConnectParameterNames.IdToken, Value = "old-id" },
            new AuthenticationToken { Name = OpenIdConnectParameterNames.RefreshToken, Value = "old-refresh" },
            new AuthenticationToken { Name = OpenIdConnectParameterNames.TokenType, Value = "Bearer" },
            new AuthenticationToken { Name = "expires_at", Value = "2026-01-01T00:00:00.0000000+00:00" },
        ]);
        return properties;
    }

    private static CookieOidcRefresher CreateRefresher(HttpStatusCode statusCode, string responseBody)
    {
        var options = new OpenIdConnectOptions
        {
            ClientId = "client",
            ClientSecret = "secret",
            ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new OpenIdConnectConfiguration
            {
                TokenEndpoint = "https://identity.example/token",
            }),
            Backchannel = new HttpClient(new StubHttpMessageHandler(statusCode, responseBody)),
            TimeProvider = TimeProvider.System,
        };
        var optionsMonitor = NSubstitute.Substitute.For<IOptionsMonitor<OpenIdConnectOptions>>();
        optionsMonitor.Get("oidc").Returns(options);
        return new CookieOidcRefresher(optionsMonitor, NullLogger<CookieOidcRefresher>.Instance);
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });
    }
}
