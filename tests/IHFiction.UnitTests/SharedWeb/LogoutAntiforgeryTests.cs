using System.Net;

using IHFiction.SharedWeb.Extensions;
using IHFiction.SharedWeb.Infrastructure;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class LogoutAntiforgeryTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Logout_RequiresPostAndValidAntiforgeryToken(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration["AllowedHosts"] = "*";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAntiforgery();
        builder.Services.AddSingleton<CookieOidcRefresher>();
        builder.Services.AddAuthentication().AddCookie("cookie").AddCookie("oidc");

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAntiforgery();
        app.MapGroup("authentication").MapLoginAndLogout("cookie", "oidc");
        app.MapGet("/token", (HttpContext context, IAntiforgery antiforgery) =>
            antiforgery.GetAndStoreTokens(context).RequestToken!);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
        var cancellationToken = TestContext.Current.CancellationToken;

        using var get = await client.GetAsync("/authentication/logout", cancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);

        using var missing = await client.PostAsync("/authentication/logout", new FormUrlEncodedContent([]), cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var token = await client.GetStringAsync("/token", cancellationToken);
        using var otherHandler = new HttpClientHandler { AllowAutoRedirect = false };
        using var otherClient = new HttpClient(otherHandler) { BaseAddress = new Uri(address) };
        using var wrongCookie = await otherClient.PostAsync("/authentication/logout", new FormUrlEncodedContent(
            [new KeyValuePair<string, string>("__RequestVerificationToken", token)]), cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, wrongCookie.StatusCode);

        using var invalid = await client.PostAsync("/authentication/logout", new FormUrlEncodedContent(
            [new KeyValuePair<string, string>("__RequestVerificationToken", "invalid")]), cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var valid = await client.PostAsync("/authentication/logout", new FormUrlEncodedContent(
            [new KeyValuePair<string, string>("__RequestVerificationToken", token)]), cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
        Assert.Equal("/", valid.Headers.Location?.OriginalString);
        Assert.Contains(valid.Headers.GetValues("Set-Cookie"), value => value.Contains(".AspNetCore.cookie=;", StringComparison.Ordinal));

        foreach (var destination in new[] { "/stories?q=Ghost&sort=title", "https://evil.test/" })
        {
            using var response = await client.PostAsync("/authentication/logout", new FormUrlEncodedContent(
                [new("__RequestVerificationToken", token), new("returnUrl", destination)]), cancellationToken);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(destination.StartsWith('/') ? destination : "/", response.Headers.Location?.OriginalString);
        }
    }
}
