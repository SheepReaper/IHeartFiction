using System.Net;
using System.Text;
using System.Text.Json;

using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Components;
using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Features.Discovery;
using IHFiction.SharedWeb.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IHFiction.UnitTests.Features.Discovery;

public sealed class TagStoriesRenderingTests
{
    [Fact]
    public async Task TagStories_RendersStoriesHeadingAndMetadataWithoutHardcodedFanfiction()
    {
        var capture = new TagStoriesCapture();
        using var handler = new TagStoriesApiHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var apiClient = new FictionApiClient(client);

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorComponents();
        builder.Services.AddSingleton<IComponentActivator>(capture);
        builder.Services.AddSingleton<NavigationManager, TestNavigation>();
        builder.Services.Configure<SiteUrlOptions>(options => options.BaseUrl = new Uri("https://example.test"));
        builder.Services.AddSingleton<MetadataUrlService>();
        builder.Services.AddSingleton<TagLandingRequestContext>();
        builder.Services.AddSingleton(new TagService(apiClient));
        builder.Services.AddSingleton(new StoryDiscoveryService(apiClient));

        await using var provider = builder.Services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<TagStories>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(TagStories.TagSpec)] = "genre:isekai" }));
            return component.ToHtmlString();
        });

        Assert.Equal("isekai Stories | I❤️Fiction", capture.Title);
        Assert.Equal("Browse published isekai stories across genres on IHeartFiction and start reading.", capture.Description);
        Assert.DoesNotContain("Fanfiction", capture.Title, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fanfiction", capture.Description, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("isekai Stories", html);
        Assert.DoesNotContain("isekai Fanfiction", html);
        Assert.Contains("href=\"/tags\"", html);
        Assert.Contains("Browse by Tag", html);

        using var schemaDoc = JsonDocument.Parse(JsonSerializer.Serialize(capture.Schema));
        var collectionPage = schemaDoc.RootElement[1];
        Assert.Equal("isekai Stories", collectionPage.GetProperty("name").GetString());
        Assert.Equal("isekai Stories", collectionPage.GetProperty("mainEntity").GetProperty("name").GetString());
    }

    [Fact]
    public async Task StoryList_WhenScopedToAuthor_OmitsBrowseByTagButton()
    {
        using var handler = new TagStoriesApiHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var apiClient = new FictionApiClient(client);

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorComponents();
        builder.Services.AddSingleton<NavigationManager, TestNavigation>();
        builder.Services.AddSingleton(new StoryDiscoveryService(apiClient));

        await using var provider = builder.Services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<StoryList>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(StoryList.AuthorId)] = Ulid.NewUlid().ToString(),
                    [nameof(StoryList.EmitPaginationHeadLinks)] = false
                }));
            return component.ToHtmlString();
        });

        Assert.DoesNotContain("Browse by Tag", html);
        Assert.DoesNotContain("browse stories by tag", html);
    }

    private sealed class TagStoriesApiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.StartsWith("/tags/resolve", StringComparison.OrdinalIgnoreCase))
            {
                var payload = JsonSerializer.Serialize(new
                {
                    canonicalSpec = "genre:isekai",
                    requiresRedirect = false,
                    storyCount = 1,
                    tags = new[]
                    {
                        new
                        {
                            tagId = Ulid.NewUlid().ToString(),
                            category = "genre",
                            subcategory = (string?)null,
                            value = "isekai",
                            display = "genre:isekai",
                            routeKey = "genre:isekai"
                        }
                    },
                    links = Array.Empty<object>()
                });

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                });
            }

            var storiesPayload = JsonSerializer.Serialize(new
            {
                data = Array.Empty<object>(),
                currentPage = 1,
                pageSize = 12,
                totalCount = 0,
                totalPages = 0,
                hasPreviousPage = false,
                hasNextPage = false,
                links = Array.Empty<object>()
            });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(storiesPayload, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://example.test/", "https://example.test/tags/genre:isekai");
    }

    private sealed class TagStoriesCapture : IComponentActivator
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public object? Schema { get; set; }

        public IComponent CreateInstance(Type componentType) => componentType switch
        {
            _ when componentType == typeof(TagStories) => new TagStories(),
            _ when componentType == typeof(StoryList) => new StoryList(),
            _ when componentType == typeof(AppBreadcrumbs) => new AppBreadcrumbs(),
            _ => new ParameterSink(this, componentType)
        };
    }

    private sealed class ParameterSink(TagStoriesCapture capture, Type componentType) : IComponent
    {
        public void Attach(RenderHandle renderHandle) { }

        public Task SetParametersAsync(ParameterView parameters)
        {
            if (componentType == typeof(SocialPreviewMetadata))
            {
                capture.Title = parameters.GetValueOrDefault<string>("Title");
                capture.Description = parameters.GetValueOrDefault<string>("Description");
            }
            else if (componentType == typeof(StructuredDataMetadata))
            {
                capture.Schema = parameters.GetValueOrDefault<object>("Schema");
            }

            return Task.CompletedTask;
        }
    }
}

