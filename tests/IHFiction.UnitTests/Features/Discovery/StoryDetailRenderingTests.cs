using System.Net;
using System.Text;
using System.Text.Json;

using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Components;
using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Features.Discovery;
using IHFiction.SharedWeb.Features.Reading;
using IHFiction.SharedWeb.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IHFiction.UnitTests.Features.Discovery;

public sealed class StoryDetailRenderingTests
{
    [Theory]
    [InlineData("MultiChapter")]
    [InlineData("SingleBody")]
    [InlineData("MultiBook")]
    public async Task RenderAsync_ExposesMobileReadingActionAndWarningSummaryBeforeDenseMetadata(string workStructure)
    {
        var storyId = Ulid.NewUlid();
        var chapter1Id = Ulid.NewUlid();
        var chapter2Id = Ulid.NewUlid();
        var book1Id = Ulid.NewUlid();
        var expectedTargetId = workStructure == "SingleBody" ? storyId : chapter1Id;
        var expectedLabel = workStructure == "SingleBody" ? "Read Story" : "Start Reading";

        var meta = new LinkedOfGetPublishedWorkMetaResponse
        {
            Id = storyId,
            StoryId = storyId,
            StoryTitle = "Magic Knows No Boundaries",
            StoryCompletionStatus = "InProgress",
            WorkType = "Story",
            ReaderKind = workStructure,
            IsDirectlyReadable = workStructure == "SingleBody",
            DefaultReadableWorkId = workStructure == "SingleBody" ? null : chapter1Id,
            ReadableChildren = workStructure == "SingleBody"
                ? []
                :
                [
                    new ReadableWorkItem
                    {
                        Id = chapter1Id,
                        Title = "Chapter 1",
                        Order = 0,
                        BookId = workStructure == "MultiBook" ? book1Id : null,
                        BookTitle = workStructure == "MultiBook" ? "Book One" : null
                    },
                    new ReadableWorkItem
                    {
                        Id = chapter2Id,
                        Title = "Chapter 2",
                        Order = 1,
                        BookId = workStructure == "MultiBook" ? book1Id : null,
                        BookTitle = workStructure == "MultiBook" ? "Book One" : null
                    }
                ],
            Authors = [],
            Links = []
        };

        using var handler = new StoryDetailHttpHandler(storyId, chapter1Id, chapter2Id, book1Id, workStructure);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorComponents();
        builder.Services.AddSingleton<IComponentActivator>(new StoryDetailComponentActivator());
        builder.Services.AddSingleton<NavigationManager>(new TestNavigation(storyId));
        builder.Services.Configure<SiteUrlOptions>(options => options.BaseUrl = new Uri("https://example.test"));
        builder.Services.AddSingleton<MetadataUrlService>();
        builder.Services.AddSingleton(new StoryDiscoveryService(new FictionApiClient(httpClient)));
        builder.Services.AddSingleton<IPublishedWorkReader>(new FakeWorkReader(meta));
        builder.Services.AddSingleton(new ReaderProgressService(new BrowserProtectedStorageService(null!)));

        await using var provider = builder.Services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var stateStore = new CapturingStateStore(new Dictionary<string, byte[]>
        {
            [$"StoryReadingLink:Meta:{storyId}"] = JsonSerializer.SerializeToUtf8Bytes(meta),
            [$"StoryReadingLink:Meta:{storyId}:Compact"] = JsonSerializer.SerializeToUtf8Bytes(meta)
        });
        await provider.GetRequiredService<ComponentStatePersistenceManager>()
            .RestoreStateAsync(stateStore);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<StoryDetail>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(StoryDetail.StoryId)] = storyId.ToString() }));
            return component.ToHtmlString();
        });

        // 1. Mobile reading action region appears after title/author and BEFORE synopsis, full warnings, and primary tags
        var titleIndex = html.IndexOf("<h1 class=\"title is-2\">Magic Knows No Boundaries</h1>", StringComparison.Ordinal);
        var mobileRegionIndex = html.IndexOf("class=\"box is-hidden-tablet mb-4\" role=\"region\" aria-label=\"Reading actions\"", StringComparison.Ordinal);
        var synopsisIndex = html.IndexOf("A long synopsis before dense tag blocks.", StringComparison.Ordinal);
        var fullWarningsIndex = html.IndexOf("id=\"story-content-warnings\"", StringComparison.Ordinal);
        var primaryTagsIndex = html.IndexOf(">Primary Tags</h2>", StringComparison.Ordinal);
        var desktopSidebarActionIndex = html.IndexOf("class=\"field is-hidden-mobile\"", StringComparison.Ordinal);

        Assert.True(titleIndex >= 0, "Title heading should be present.");
        Assert.True(mobileRegionIndex > titleIndex, "Mobile reading action region should appear after the title.");
        Assert.True(synopsisIndex > mobileRegionIndex, "Mobile reading action region should appear before the story synopsis.");
        Assert.True(fullWarningsIndex > synopsisIndex, "Full content warnings section should appear after the synopsis.");
        Assert.True(primaryTagsIndex > fullWarningsIndex, "Primary tags should appear after content warnings.");
        Assert.True(desktopSidebarActionIndex > primaryTagsIndex, "Desktop sidebar reading action should remain in the sidebar column.");

        // 2. Concise warning summary and jump link to #story-content-warnings appear inside the mobile region before prose
        var mobileRegionSlice = html[mobileRegionIndex..synopsisIndex];
        Assert.Contains($"href=\"/read/{expectedTargetId}\"", mobileRegionSlice, StringComparison.Ordinal);
        Assert.Contains(expectedLabel, mobileRegionSlice, StringComparison.Ordinal);
        Assert.Contains("Content Warnings (4):", mobileRegionSlice, StringComparison.Ordinal);
        Assert.Contains("alcohol use, animal distress, bereavement, and 1 more", mobileRegionSlice, StringComparison.Ordinal);
        Assert.Contains($"href=\"/stories/{storyId}#story-content-warnings\"", mobileRegionSlice, StringComparison.Ordinal);
        Assert.Contains("Review all content warnings", mobileRegionSlice, StringComparison.Ordinal);

        // 3. Desktop sidebar also exposes the same authoritative target and warning summary
        var desktopSlice = html[desktopSidebarActionIndex..];
        Assert.Contains($"href=\"/read/{expectedTargetId}\"", desktopSlice, StringComparison.Ordinal);
        Assert.Contains(expectedLabel, desktopSlice, StringComparison.Ordinal);
    }

    private sealed class StoryDetailHttpHandler(
        Ulid storyId,
        Ulid chapter1Id,
        Ulid chapter2Id,
        Ulid book1Id,
        string workStructure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith($"/stories/{storyId}/chapters", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse(new
                {
                    data = new[]
                    {
                        new
                        {
                            chapterId = chapter1Id.ToString(),
                            title = "Chapter 1",
                            order = 0,
                            publishedAt = "2026-03-15T12:00:00Z",
                            createdAt = "2026-03-15T12:00:00Z",
                            updatedAt = "2026-03-15T12:00:00Z",
                            hasContent = true,
                            readCount = 5,
                            links = Array.Empty<object>()
                        },
                        new
                        {
                            chapterId = chapter2Id.ToString(),
                            title = "Chapter 2",
                            order = 1,
                            publishedAt = "2026-03-16T12:00:00Z",
                            createdAt = "2026-03-16T12:00:00Z",
                            updatedAt = "2026-03-16T12:00:00Z",
                            hasContent = true,
                            readCount = 3,
                            links = Array.Empty<object>()
                        }
                    },
                    totalCount = 2,
                    currentPage = 1,
                    pageSize = 50,
                    totalPages = 1,
                    links = Array.Empty<object>()
                }));
            }

            return Task.FromResult(JsonResponse(new
            {
                id = storyId.ToString(),
                title = "Magic Knows No Boundaries",
                description = "A long synopsis before dense tag blocks.",
                completionStatus = "InProgress",
                publishedAt = "2026-03-15T12:00:00Z",
                isPublished = true,
                updatedAt = "2026-03-16T12:00:00Z",
                createdAt = "2026-03-10T12:00:00Z",
                ownerId = Ulid.NewUlid().ToString(),
                ownerName = "nonsensicalrants",
                type = workStructure,
                hasCoverImage = false,
                authors = new[]
                {
                    new { id = Ulid.NewUlid().ToString(), name = "nonsensicalrants" }
                },
                tags = new[]
                {
                    new { category = "content_warning", subcategory = (string?)"animal_welfare", value = "animal_distress", routeKey = "content-warning:animal-welfare:animal-distress" },
                    new { category = "content_warning", subcategory = (string?)"emotion", value = "bereavement", routeKey = "content-warning:emotion:bereavement" },
                    new { category = "content_warning", subcategory = (string?)"substance", value = "alcohol_use", routeKey = "content-warning:substance:alcohol-use" },
                    new { category = "content_warning", subcategory = (string?)"violence", value = "references_to_war", routeKey = "content-warning:violence:references-to-war" },
                    new { category = "genre", subcategory = (string?)null, value = "adventure", routeKey = "genre:adventure" },
                    new { category = "fandom", subcategory = (string?)null, value = "harry_potter", routeKey = "fandom:harry-potter" }
                },
                books = workStructure == "MultiBook"
                    ? new[]
                    {
                        new
                        {
                            id = book1Id.ToString(),
                            title = "Book One",
                            description = "First volume",
                            order = 0,
                            readCount = 8,
                            chapters = new[]
                            {
                                new { id = chapter1Id.ToString(), title = "Chapter 1", order = 0, readCount = 5 },
                                new { id = chapter2Id.ToString(), title = "Chapter 2", order = 1, readCount = 3 }
                            }
                        }
                    }
                    : Array.Empty<object>(),
                chapters = workStructure == "MultiChapter"
                    ? new[]
                    {
                        new { id = chapter1Id.ToString(), title = "Chapter 1", order = 0, readCount = 5 },
                        new { id = chapter2Id.ToString(), title = "Chapter 2", order = 1, readCount = 3 }
                    }
                    : Array.Empty<object>(),
                readCount = 8,
                links = Array.Empty<object>()
            }));
        }

        private static HttpResponseMessage JsonResponse(object payload) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    private sealed class FakeWorkReader(LinkedOfGetPublishedWorkMetaResponse meta) : IPublishedWorkReader
    {
        public ValueTask<Result<LinkedOfGetPublishedWorkMetaResponse>> GetPublishedWorkMetaAsync(
            Ulid id,
            string? fields = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Result.Success(meta));

        public ValueTask<Result<LinkedOfGetPublishedWorkContentResponse>> GetPublishedWorkContentAsync(
            Ulid id,
            string? fields = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Story detail must not fetch work body content.");
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation(Ulid storyId) =>
            Initialize("https://example.test/", $"https://example.test/stories/{storyId}");
    }

    private sealed class StoryDetailComponentActivator : IComponentActivator
    {
        public IComponent CreateInstance(Type componentType)
        {
            if (componentType == typeof(SocialPreviewMetadata)
                || componentType == typeof(StructuredDataMetadata)
                || componentType == typeof(IHFiction.SharedWeb.Features.Following.StoryFollowButton)
                || componentType == typeof(DisqusThread))
            {
                return new NoOpComponent();
            }

            return (IComponent)Activator.CreateInstance(componentType)!;
        }
    }

    private sealed class NoOpComponent : IComponent
    {
        public void Attach(RenderHandle renderHandle) { }
        public Task SetParametersAsync(ParameterView parameters) => Task.CompletedTask;
    }

    private sealed class CapturingStateStore(IDictionary<string, byte[]> state) : IPersistentComponentStateStore
    {
        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() =>
            Task.FromResult(state);
        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state) => Task.CompletedTask;
        public bool SupportsRenderMode(IComponentRenderMode renderMode) => true;
    }
}
