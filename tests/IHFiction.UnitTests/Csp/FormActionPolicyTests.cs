using System.Security.Claims;

using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Csp;
using IHFiction.SharedWeb.Features.Reading;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IHFiction.UnitTests.Csp;

public sealed class FormActionPolicyTests
{
    [Theory]
    [InlineData(false, "text/html", false)]
    [InlineData(false, "text/html; charset=utf-8", false)]
    [InlineData(true, "text/html", true)]
    [InlineData(true, "text/html; charset=utf-8", true)]
    [InlineData(true, "text/plain", false)]
    [InlineData(true, "application/json", false)]
    [InlineData(true, "text/html-other", false)]
    public async Task IdentityProviderFormActions_AreLimitedToAuthenticatedHtmlDocuments(
        bool authenticated, string contentType, bool allowsIdentityProvider)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["AllowedHosts"] = "*";
        builder.Configuration["OidcAuthority"] = "https://auth.example.test/realms/fiction";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.Configure<SiteUrlOptions>(o => o.BaseUrl = new Uri("https://example.test"));
        builder.Services.Configure<DisqusOptions>(o => o.ShortName = "example");
        builder.Services.AddAuthentication("cookie").AddCookie("cookie");
        builder.Services.AddCspProvider();
        await using var app = builder.Build();
        app.UseCsp();
        app.UseAuthentication();
        app.MapGet("/sign-in", async (HttpContext context) =>
        {
            await context.SignInAsync("cookie", new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "test-user")], "cookie")));
            return Results.NoContent();
        });
        app.MapGet("/document", () => Results.Content("document", contentType));
        var cancellationToken = TestContext.Current.CancellationToken;
        await app.StartAsync(cancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var handler = new HttpClientHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
        if (authenticated)
        {
            using var signIn = await client.GetAsync("/sign-in", cancellationToken);
            Assert.True(signIn.IsSuccessStatusCode);
        }

        using var response = await client.GetAsync("/document", cancellationToken);
        Assert.True(response.IsSuccessStatusCode);
        var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        var formAction = policy.Split(';').Single(d => d.TrimStart().StartsWith("form-action ", StringComparison.Ordinal)).Trim();
        Assert.Equal(allowsIdentityProvider ? "form-action 'self' https://auth.example.test" : "form-action 'self'", formAction);
        Assert.Contains("frame-ancestors 'self';", policy);
    }
}
