using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Csp;
using IHFiction.SharedWeb.Features.Reading;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace IHFiction.UnitTests.Csp;

public sealed class RemoteSignOutFramingTests
{
    [Theory]
    [InlineData("/signout-oidc", "/signout-oidc", "GET", true)]
    [InlineData("/signout-oidc", "/signout-oidc?iss=https://untrusted.test", "POST", true)]
    [InlineData("/custom-signout", "/custom-signout", "GET", true)]
    [InlineData("/custom-signout", "/base/custom-signout", "GET", true)]
    [InlineData("/custom-signout", "/signout-oidc", "GET", false)]
    [InlineData("/signout-oidc", "/signout-oidc/other", "GET", false)]
    [InlineData("/signout-oidc", "/authentication/logout", "GET", false)]
    [InlineData("/signout-oidc", "/", "GET", false)]
    [InlineData("", "/signout-oidc", "GET", false)]
    public async Task OnlyConfiguredRemoteSignOut_AllowsIdentityProviderFraming(
        string remoteSignOutPath, string requestPath, string method, bool allowsIdentityProvider)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["AllowedHosts"] = "*";
        builder.Configuration["OidcAuthority"] = "https://auth.example.test:8443/realms/fiction";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.Configure<SiteUrlOptions>(o => o.BaseUrl = new Uri("https://example.test"));
        builder.Services.Configure<DisqusOptions>(o => o.ShortName = "example");
        builder.Services.AddCspProvider();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie()
            .AddOpenIdConnect("oidc", o =>
            {
                o.ClientId = "test-client";
                o.Authority = builder.Configuration["OidcAuthority"];
                o.Configuration = new OpenIdConnectConfiguration { Issuer = o.Authority };
                o.RemoteSignOutPath = remoteSignOutPath;
            });

        await using var app = builder.Build();
        app.UsePathBase("/base");
        app.Use((context, next) =>
        {
            // Keep the production proxy's legacy header to verify CSP precedence separately in a browser.
            context.Response.Headers.XFrameOptions = "SAMEORIGIN";
            return next(context);
        });
        app.UseCsp();
        app.UseAuthentication();
        app.Run(context => context.Response.WriteAsync("ordinary page"));
        var cancellationToken = TestContext.Current.CancellationToken;
        await app.StartAsync(cancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        using var request = new HttpRequestMessage(new HttpMethod(method), requestPath);
        using var response = await client.SendAsync(request, cancellationToken);

        Assert.True(response.IsSuccessStatusCode);
        var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        var ancestors = policy.Split(';').Single(d => d.TrimStart().StartsWith("frame-ancestors ", StringComparison.Ordinal)).Trim();
        Assert.Equal(allowsIdentityProvider
            ? "frame-ancestors 'self' https://auth.example.test:8443"
            : "frame-ancestors 'self'", ancestors);
        Assert.Equal("SAMEORIGIN", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.DoesNotContain("untrusted.test", policy);
        Assert.Contains("object-src 'none'", policy);
    }
}
