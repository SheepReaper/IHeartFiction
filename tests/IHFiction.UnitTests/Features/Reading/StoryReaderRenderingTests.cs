using IHFiction.SharedWeb.Services;
using FluentAssertions;

using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Features.Reading;

using Markdig;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IHFiction.UnitTests.Features.Reading;

public sealed class StoryReaderRenderingTests
{
    [Theory]
    [InlineData("Story")]
    [InlineData("Chapter")]
    [InlineData("Book")]
    [InlineData("ChapteredStory")]
    [InlineData("Persisted")]
    [InlineData("Lowercase")]
    [InlineData("Unavailable")]
    [InlineData("EmptyContainer")]
    public async Task RenderAsync_CompletesWithBodyOrUnavailableStateWithoutBrowserExecution(string kind)
    {
        var storyId = Ulid.NewUlid();
        var chapterId = Ulid.NewUlid();
        var nextChapterId = Ulid.NewUlid();
        var meta = new LinkedOfGetPublishedWorkMetaResponse
        {
            Id = chapterId,
            Title = "Chapter One",
            StoryId = storyId,
            StoryTitle = "Test Story",
            WorkType = "Chapter",
            ReaderKind = "Chapter",
            IsDirectlyReadable = true,
            Authors = [new WorkAuthor { Id = Ulid.NewUlid(), Name = "Test Author" }],
            ReadableChildren =
            [
                new ReadableWorkItem { Id = chapterId, Title = "Chapter One", Order = 0 },
                new ReadableWorkItem { Id = nextChapterId, Title = "Chapter Two", Order = 1 }
            ],
            Links = []
        };
        var content = new LinkedOfGetPublishedWorkContentResponse
        {
            Id = chapterId,
            Title = "Chapter One",
            WorkType = "Chapter",
            StoryId = storyId,
            StoryTitle = "Test Story",
            Content = "## Complete body\n\nThe full chapter text.",
            Note1 = "Opening note",
            Note2 = "Closing note",
            ContentUpdatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Links = []
        };

        var requestedMeta = kind is "Book" or "ChapteredStory" or "EmptyContainer"
            ? new LinkedOfGetPublishedWorkMetaResponse
            {
                Id = storyId, Title = "Container", WorkType = kind == "Book" ? "Book" : "Story",
                IsDirectlyReadable = false, DefaultReadableWorkId = chapterId,
                ReadableChildren = meta.ReadableChildren, Authors = [], Links = []
            }
            : meta;
        if (kind == "Story") meta.WorkType = "Story";
        if (kind == "EmptyContainer") requestedMeta.DefaultReadableWorkId = null;
        var fakeReader = new FakePublishedWorkReader(meta, content)
        {
            Unavailable = kind is "Unavailable" or "Persisted"
        };
        await using var services = CreateServices(fakeReader);
        if (kind == "Persisted")
        {
            var model = new MarkdownReaderModel(chapterId.ToString(), content);
            await services.GetRequiredService<Microsoft.AspNetCore.Components.Infrastructure.ComponentStatePersistenceManager>()
                .RestoreStateAsync(new MemoryStateStore(new Dictionary<string, byte[]>
                {
                    [$"StoryReader:Resolved:{requestedMeta.Id}"] = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(model)
                }));
        }
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<StoryReader>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(StoryReader.WorkId)] = kind == "Lowercase"
                        ? requestedMeta.Id.ToString().ToLowerInvariant() : requestedMeta.Id.ToString(),
                    [nameof(StoryReader.Meta)] = requestedMeta
                }));
            return component.ToHtmlString();
        });

        if (kind is "Unavailable" or "EmptyContainer" or "Book" or "ChapteredStory")
        {
            if (kind != "Unavailable") fakeReader.ContentCalls.Should().Be(0);
            html.Should().Contain("Error:");
            html.Should().NotContain("The full chapter text.");
            html.Should().NotContain("Loading...");
            return;
        }
        if (kind == "Persisted") fakeReader.ContentCalls.Should().Be(0);
        html.Should().Contain("Chapter One");
        html.Should().Contain($"/authors/{meta.Authors.Single().Id}");
        html.Should().Contain("Opening note");
        html.Should().Contain("Complete body");
        html.Should().Contain("The full chapter text.");
        html.Should().Contain("Closing note");
        html.Should().Contain($"/read/{nextChapterId}");
        if (kind == "Chapter")
        {
            html.Should().Contain("Chapter 1 of 2");
            html.Should().Contain("aria-label=\"Jump to chapter\"");
            html.Should().Contain("Chapter 1: Chapter One");
            html.Should().Contain("Chapter 2: Chapter Two");
        }
        html.Should().NotContain("Loading...");
        html.Should().NotContain("Finding your place...");
    }

    [Fact]
    public async Task ParameterChange_DiscardsOldBodyThatCompletesAfterNewBody()
    {
        var oldId = Ulid.NewUlid();
        var newId = Ulid.NewUlid();
        var pending = new TaskCompletionSource<Result<LinkedOfGetPublishedWorkContentResponse>>();
        var oldMeta = new LinkedOfGetPublishedWorkMetaResponse
        {
            Id = oldId, Title = "Old title", IsDirectlyReadable = true,
            WorkType = "Story", Authors = [], ReadableChildren = [], Links = []
        };
        var newMeta = new LinkedOfGetPublishedWorkMetaResponse
        {
            Id = newId, Title = "New title", IsDirectlyReadable = true,
            WorkType = "Story", Authors = [], ReadableChildren = [], Links = []
        };
        var newContent = new LinkedOfGetPublishedWorkContentResponse { Id = newId, Content = "New body", Links = [] };
        var fakeReader = new FakePublishedWorkReader(oldMeta, newContent)
        {
            ResolveContent = id => id == oldId ? new ValueTask<Result<LinkedOfGetPublishedWorkContentResponse>>(pending.Task)
                : ValueTask.FromResult(Result.Success(newContent))
        };
        var activator = new CapturingActivator();
        await using var services = CreateServices(fakeReader, activator);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = renderer.BeginRenderingComponent<StoryReader>(Parameters(oldMeta));
            await activator.Reader!.SetParametersAsync(Parameters(newMeta));
            root.ToHtmlString().Should().Contain("New body").And.NotContain("Old title");
            pending.SetResult(Result.Success(new LinkedOfGetPublishedWorkContentResponse
            {
                Id = oldId, Content = "Old body", Links = []
            }));
            await root.QuiescenceTask;
            root.ToHtmlString().Should().Contain("New title").And.Contain("New body").And.NotContain("Old body");
        });
    }

    [Theory]
    [InlineData("InProgress", "You're caught up with the latest published chapter. This story is still in progress.", "You've reached the end of this story.")]
    [InlineData("Complete", "You've reached the end of this story.", "You're caught up with the latest published chapter. This story is still in progress.")]
    public async Task RenderAsync_FinalPublishedChapter_DistinguishesCaughtUpFromCompletedStory(
        string storyCompletionStatus,
        string expectedBannerText,
        string unexpectedBannerText)
    {
        var storyId = Ulid.NewUlid();
        var chapter1Id = Ulid.NewUlid();
        var chapter2Id = Ulid.NewUlid();
        var meta = new LinkedOfGetPublishedWorkMetaResponse
        {
            Id = chapter2Id,
            Title = "Chapter Two",
            StoryId = storyId,
            StoryTitle = "Ongoing Odyssey",
            StoryCompletionStatus = storyCompletionStatus,
            WorkType = "Chapter",
            ReaderKind = "Chapter",
            IsDirectlyReadable = true,
            Authors = [new WorkAuthor { Id = Ulid.NewUlid(), Name = "Test Author" }],
            ReadableChildren =
            [
                new ReadableWorkItem { Id = chapter1Id, Title = "Chapter One", Order = 0 },
                new ReadableWorkItem { Id = chapter2Id, Title = "Chapter Two", Order = 1 }
            ],
            Links = []
        };
        var content = new LinkedOfGetPublishedWorkContentResponse
        {
            Id = chapter2Id,
            Title = "Chapter Two",
            WorkType = "Chapter",
            StoryId = storyId,
            StoryTitle = "Ongoing Odyssey",
            Content = "The latest published chapter text.",
            ContentUpdatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Links = []
        };

        var fakeReader = new FakePublishedWorkReader(meta, content);
        await using var services = CreateServices(fakeReader);
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<StoryReader>(Parameters(meta));
            return component.ToHtmlString();
        });

        html.Should().Contain(expectedBannerText);
        html.Should().NotContain(unexpectedBannerText);
        html.Should().Contain("Back to Story Details");
        html.Should().Contain($"href=\"/stories/{storyId}\"");
    }

    [Fact]
    public async Task RenderAsync_CrossBookTransitionChapter_ShowsContinueToNextBookInsteadOfEndBanner()
    {
        var storyId = Ulid.NewUlid();
        var book1Id = Ulid.Parse("01J00000000000000000000001");
        var book2Id = Ulid.Parse("01J00000000000000000000002");
        var chapter1Id = Ulid.NewUlid();
        var chapter2Id = Ulid.NewUlid();
        var meta = new LinkedOfGetPublishedWorkMetaResponse
        {
            Id = chapter1Id,
            Title = "Book One Finale",
            StoryId = storyId,
            StoryTitle = "Saga",
            StoryCompletionStatus = "InProgress",
            BookId = book1Id,
            BookTitle = "Book One",
            WorkType = "Chapter",
            ReaderKind = "Chapter",
            IsDirectlyReadable = true,
            Authors = [],
            ReadableChildren =
            [
                new ReadableWorkItem { Id = chapter1Id, Title = "Book One Finale", Order = 0, BookId = book1Id, BookTitle = "Book One" },
                new ReadableWorkItem { Id = chapter2Id, Title = "Book Two Opening", Order = 0, BookId = book2Id, BookTitle = "Book Two" }
            ],
            Links = []
        };
        var content = new LinkedOfGetPublishedWorkContentResponse
        {
            Id = chapter1Id,
            Title = "Book One Finale",
            WorkType = "Chapter",
            StoryId = storyId,
            StoryTitle = "Saga",
            Content = "End of book one.",
            ContentUpdatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            Links = []
        };

        var fakeReader = new FakePublishedWorkReader(meta, content);
        await using var services = CreateServices(fakeReader);
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<StoryReader>(Parameters(meta));
            return component.ToHtmlString();
        });

        html.Should().Contain("You finished Book One. Next up: Book Two.");
        html.Should().Contain("Continue to Book Two");
        html.Should().Contain($"href=\"/read/{chapter2Id}\"");
        html.Should().NotContain("You're caught up with the latest published chapter.");
        html.Should().NotContain("You've reached the end of this story.");
    }

    private static ParameterView Parameters(LinkedOfGetPublishedWorkMetaResponse meta) =>
        ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(StoryReader.WorkId)] = meta.Id.ToString(),
            [nameof(StoryReader.Meta)] = meta
        });

    private static ServiceProvider CreateServices(IPublishedWorkReader fakeReader, CapturingActivator? activator = null)
    {
        return Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder().Services
            .AddLogging()
            .AddSingleton<IComponentActivator>(activator ?? new CapturingActivator())
            .AddSingleton<IPublishedWorkReader>(fakeReader)
            .AddTransient<MarkdownReaderService>()
            .AddSingleton<Microsoft.JSInterop.IJSRuntime>(new NoJavaScript())
            .AddSingleton<ThemeService>()
            .AddSingleton(new ReaderProgressService(new BrowserProtectedStorageService(null!)))
            .AddSingleton(new ReadTrackingService(null!, null!))
            .AddSingleton<NavigationManager>(new TestNavigation())
            .AddRazorComponents().Services
            .AddSingleton(new MarkdownPipelineBuilder().Build())
            .BuildServiceProvider();
    }

    private sealed class CapturingActivator : IComponentActivator
    {
        public StoryReader? Reader { get; private set; }
        public IComponent CreateInstance(Type componentType)
        {
            var component = (IComponent)Activator.CreateInstance(componentType)!;
            if (component is StoryReader reader) Reader = reader;
            return component;
        }
    }

    private sealed class FakePublishedWorkReader(LinkedOfGetPublishedWorkMetaResponse meta, LinkedOfGetPublishedWorkContentResponse content)
        : IPublishedWorkReader
    {
        public bool Unavailable { get; init; }
        public Func<Ulid, ValueTask<Result<LinkedOfGetPublishedWorkContentResponse>>>? ResolveContent { get; init; }
        public int ContentCalls { get; private set; }
        public ValueTask<Result<LinkedOfGetPublishedWorkMetaResponse>> GetPublishedWorkMetaAsync(
            Ulid id,
            string? fields = null,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Result.Success(meta));

        public ValueTask<Result<LinkedOfGetPublishedWorkContentResponse>> GetPublishedWorkContentAsync(
            Ulid id,
            string? fields = null,
            CancellationToken cancellationToken = default)
        {
            ContentCalls++;
            if (ResolveContent is not null) return ResolveContent(id);
            return ValueTask.FromResult(Unavailable
                ? Result.Failure<LinkedOfGetPublishedWorkContentResponse>(DomainError.NotFound)
                : Result.Success(content));
        }
    }
    private sealed class MemoryStateStore(IDictionary<string, byte[]> state) : IPersistentComponentStateStore
    {
        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() => Task.FromResult(state);
        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state) => Task.CompletedTask;
        public bool SupportsRenderMode(IComponentRenderMode renderMode) => true;
    }

    private sealed class NoJavaScript : Microsoft.JSInterop.IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("JavaScript must not run during server rendering.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException("JavaScript must not run during server rendering.");
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://localhost/", "https://localhost/read/test");
        protected override void NavigateToCore(string uri, bool forceLoad) =>
            throw new InvalidOperationException("Navigation must not run during server rendering.");
    }

}
