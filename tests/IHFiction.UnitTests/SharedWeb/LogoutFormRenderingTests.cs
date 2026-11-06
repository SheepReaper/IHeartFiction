using System.Security.Claims;

using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Features.AccountManagement;
using IHFiction.SharedWeb.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class LogoutFormRenderingTests
{
    [Theory]
    [InlineData("/stories?q=Ghost&sort=title", "/stories?q=Ghost&amp;sort=title")]
    [InlineData("/", "/")]
    [InlineData(null, "/")]
    public async Task AuthenticatedMenu_PostsCascadedDestinationWithAntiforgeryToken(string? destination, string expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddSingleton<AuthenticationStateProvider, SignedInState>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton<LoaderService>();
        services.AddSingleton<AntiforgeryStateProvider, TokenState>();
        using var httpClient = new HttpClient();
        services.AddSingleton(new AccountService(new FictionApiClient(httpClient)));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<CascadingValue<string>>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(CascadingValue<>.Name)] = "LogoutReturnUrl",
                    [nameof(CascadingValue<>.Value)] = destination,
                    [nameof(CascadingValue<>.ChildContent)] = (RenderFragment)(builder =>
                    {
                        builder.OpenComponent<LoginDisplay>(0);
                        builder.CloseComponent();
                    })
                }));
            return component.ToHtmlString();
        });
        Assert.Contains("action=\"authentication/logout\" method=\"post\"", html);
        Assert.Contains($"name=\"returnUrl\" value=\"{expected}\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\" value=\"test-token\"", html);
    }

    [Fact]
    public async Task Unauthenticated_RendersNavigableSignUpAndLogInLinks()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddSingleton<AuthenticationStateProvider, SignedOutState>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton<LoaderService>();
        services.AddSingleton<AntiforgeryStateProvider, TokenState>();
        using var httpClient = new HttpClient();
        services.AddSingleton(new AccountService(new FictionApiClient(httpClient)));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<LoginDisplay>(ParameterView.Empty);
            return component.ToHtmlString();
        });

        Assert.Contains("class=\"button is-primary\"", html);
        Assert.Contains("role=\"button\"", html);
        Assert.Contains("href=\"/authentication/register?returnUrl=%2Fstories\"", html);
        Assert.Contains("<strong>Sign up</strong>", html);
        Assert.Contains("class=\"button has-background-link-soft has-text-link-bold\"", html);
        Assert.Contains("href=\"/authentication/login?returnUrl=%2Fstories\"", html);
        Assert.Contains("Log in", html);
        Assert.DoesNotContain("<button class=\"button is-primary\"", html);
        Assert.DoesNotContain("<button class=\"button has-background-link-soft", html);
    }

    private sealed class SignedOutState : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private sealed class SignedInState : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Reader")], "test"))));
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://example.test/", "https://example.test/stories");
    }

    private sealed class TokenState : AntiforgeryStateProvider
    {
        public override AntiforgeryRequestToken GetAntiforgeryToken() => new("test-token", "__RequestVerificationToken");
    }
}
