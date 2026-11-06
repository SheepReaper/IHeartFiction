using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

using FluentAssertions;

using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Features.Discovery;
using IHFiction.SharedWeb.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IHFiction.SharedWeb.Configuration;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class HomeRenderingTests
{
    [Fact]
    public async Task Home_RendersHeroAndNavigationCallsToAction()
    {
        var builder = WebApplication.CreateBuilder();
        var services = builder.Services;
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider());
        services.AddRazorComponents();
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton(new MetadataUrlService(Options.Create(new SiteUrlOptions { BaseUrl = new Uri("https://example.test/") })));

        var storyJson = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new
                {
                    storyId = "01K915FJS73S23P1VJ5WW1MG4Q",
                    title = "Test Story",
                    description = "A captivating tale of test fixtures and magic.",
                    authorId = "01K915D97DWAWMDJVXFFRQMAEN",
                    authorName = "Author One",
                    hasCoverImage = false,
                    completionStatus = "InProgress",
                    hasChapters = true,
                    chapterCount = 2,
                    readCount = 42,
                    totalWords = 5000,
                    publishedAt = DateTimeOffset.UtcNow,
                    updatedAt = DateTimeOffset.UtcNow,
                    primaryTaxonomy = new[] { "genre:fantasy", "fandom:harry_potter" }
                }
            },
            currentPage = 1,
            pageSize = 6,
            totalCount = 1,
            totalPages = 1,
            links = Array.Empty<object>()
        });

        var tagJson = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new
                {
                    category = "fandom",
                    subcategory = "book",
                    value = "harry_potter",
                    routeKey = "fandom:book:harry_potter",
                    storyCount = 5
                }
            },
            currentPage = 1,
            pageSize = 30,
            totalCount = 1,
            totalPages = 1,
            links = Array.Empty<object>()
        });

        var handler = new StubHandler((req, ct) =>
        {
            var path = req.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("/stories/published") || path.Contains("/stories"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(storyJson, Encoding.UTF8, "application/json")
                };
            }
            if (path.Contains("/tags"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(tagJson, Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") };
        var apiClient = new FictionApiClient(httpClient);
        services.AddSingleton(new StoryDiscoveryService(apiClient));
        services.AddSingleton(new TagService(apiClient));

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<Home>(ParameterView.Empty);
            return component.ToHtmlString();
        });

        // Hero and CTAs
        html.Should().Contain("Welcome to I❤️Fiction");
        html.Should().Contain("href=\"/stories\"");
        html.Should().Contain("href=\"/tags\"");
        html.Should().Contain("href=\"/become-author\"");
        html.Should().Contain("Browse Stories");
        html.Should().Contain("Explore Tags");
        html.Should().Contain("Start Writing");

        // Sections
        html.Should().Contain("Recently Updated Stories");
        html.Should().Contain("Browse by Genre &amp; Fandom");
        html.Should().Contain("class=\"tag is-info\"");
        html.Should().Contain("harry potter");
        html.Should().Contain("book");
        html.Should().Contain("Write on I❤️Fiction");
        html.Should().Contain("Become an Author");
    }

    private sealed class TestAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://example.test/", "https://example.test/");
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request, cancellationToken));
    }
}
