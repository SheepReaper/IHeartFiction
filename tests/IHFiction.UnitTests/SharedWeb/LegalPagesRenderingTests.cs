using System.Security.Claims;
using FluentAssertions;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Features.AccountManagement;
using IHFiction.SharedWeb.Features.Legal;
using IHFiction.SharedWeb.Layout;
using IHFiction.SharedWeb.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class LegalPagesRenderingTests
{
    [Fact]
    public async Task PrivacyPage_RendersComprehensivePolicyAndDisclosures()
    {
        var services = CreateServices();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<Privacy>(ParameterView.Empty);
            return component.ToHtmlString();
        });

        html.Should().Contain("Privacy Policy");
        html.Should().Contain("Bryan Gonzalez");
        html.Should().Contain("privacy@iheartfiction.net");
        html.Should().Contain("Cloudflare");
        html.Should().Contain("Disqus");
        html.Should().Contain("MailerSend");
        html.Should().Contain("Google reCAPTCHA");
        html.Should().Contain("Gravatar");
        html.Should().Contain("GDPR");
        html.Should().Contain("right to be forgotten");
        html.Should().Contain("href=\"/terms\"");
    }

    [Fact]
    public async Task TermsPage_RendersAuthorOwnershipAndContentStandards()
    {
        var services = CreateServices();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<Terms>(ParameterView.Empty);
            return component.ToHtmlString();
        });

        html.Should().Contain("Terms of Service &amp; Author Rights");
        html.Should().Contain("100% of your copyright");
        html.Should().Contain("MIT software license");
        html.Should().Contain("No AI Model Training");
        html.Should().Contain("Originality &amp; Plagiarism");
        html.Should().Contain("Transformative &amp; Fan Works");
        html.Should().Contain("Human Moderation");
        html.Should().Contain("href=\"/privacy\"");
        html.Should().Contain("support@iheartfiction.net");
    }

    [Fact]
    public async Task AppFooter_RendersGlobalFooterWithPolicyLinks()
    {
        var services = CreateServices();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<AppFooter>(ParameterView.Empty);
            return component.ToHtmlString();
        });

        html.Should().Contain("<footer class=\"footer");
        html.Should().Contain("href=\"/privacy\"");
        html.Should().Contain("href=\"/terms\"");
        html.Should().Contain("href=\"/become-author\"");
        html.Should().Contain("href=\"/about\"");
        html.Should().Contain("https://github.com/SheepReaper/IHeartFiction");
    }

    [Fact]
    public async Task BecomeAuthor_WhenAnonymous_RendersGuidelinesAndLoginCallsToAction()
    {
        var services = CreateServices();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<BecomeAuthor>(ParameterView.Empty);
            return component.ToHtmlString();
        });

        html.Should().Contain("Become an Author");
        html.Should().Contain("Your Responsibilities");
        html.Should().Contain("Our Commitments");
        html.Should().Contain("href=\"/signup?returnUrl=%2Fbecome-author\"");
        html.Should().Contain("href=\"/login?returnUrl=%2Fbecome-author\"");
        html.Should().Contain("Create Author Account");
    }

    [Fact]
    public async Task BecomeAuthor_WhenAlreadyAuthor_RendersAlreadyRegisteredBanner()
    {
        var authorClaims = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "testauthor"),
            new Claim(ClaimTypes.Role, "author")
        ], "testauth"));

        var services = CreateServices(authorClaims);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<BecomeAuthor>(ParameterView.Empty);
            return component.ToHtmlString();
        });

        html.Should().Contain("You are already registered as an author!");
        html.Should().Contain("href=\"/my-stories\"");
        html.Should().Contain("href=\"/stories/new\"");
    }

    private static IServiceCollection CreateServices(ClaimsPrincipal? principal = null)
    {
        var builder = WebApplication.CreateBuilder();
        var services = builder.Services;
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddRazorComponents();
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton(new MetadataUrlService(Options.Create(new SiteUrlOptions { BaseUrl = new Uri("https://example.test/") })));
        services.AddScoped<LoaderService>();

        var authStateProvider = new TestAuthStateProvider(principal ?? new ClaimsPrincipal(new ClaimsIdentity()));
        services.AddSingleton<AuthenticationStateProvider>(authStateProvider);

        var handler = new StubHandler((req, ct) => new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") };
        var apiClient = new FictionApiClient(httpClient);
        services.AddSingleton(new AccountService(apiClient));

        return services;
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://example.test/", "https://example.test/");
    }

    private sealed class TestAuthStateProvider(ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request, cancellationToken));
    }
}
