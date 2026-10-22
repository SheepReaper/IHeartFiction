using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Components.Reading;
using IHFiction.SharedWeb.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class StoryReadingLinkTests
{
    [Theory]
    [InlineData("Single")]
    [InlineData("Chapters")]
    [InlineData("Books")]
    [InlineData("Empty")]
    [InlineData("Resume")]
    [InlineData("Default")]
    [InlineData("Stale")]
    [InlineData("Invalid")]
    [InlineData("UnavailableStorage")]
    public async Task ServerLink_IsDirectAndHydrationOnlyPersonalizesEligibleProgress(string scenario)
    {
        var id = Ulid.NewUlid();
        var first = Ulid.NewUlid();
        var second = Ulid.NewUlid();
        var meta = Meta(id, first, second);
        if (scenario == "Single") meta.IsDirectlyReadable = true;
        if (scenario == "Books")
        {
            meta.DefaultReadableWorkId = second;
            meta.ReadableChildren.First().BookId = Ulid.NewUlid();
            meta.ReadableChildren.Last().BookId = Ulid.NewUlid();
        }
        if (scenario == "Empty") { meta.DefaultReadableWorkId = null; meta.ReadableChildren = []; }
        var js = new StorageRuntime();
        var progress = new ReaderProgressService(new BrowserProtectedStorageService(
            new ProtectedLocalStorage(js, new EphemeralDataProtectionProvider())));
        var remembered = scenario switch
        {
            "Resume" => second.ToString(),
            "Default" => first.ToString(),
            "Stale" => Ulid.NewUlid().ToString(),
            "Invalid" => "not-a-work-id",
            _ => null
        };
        if (remembered is not null) await progress.SetLastReadChapterIdAsync(id.ToString(), remembered);
        js.Unavailable = scenario == "UnavailableStorage";
        var activator = new LinkActivator();
        await using var services = Services(new MetaReader(meta), progress, activator);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<StoryReadingLink>(Parameters(id));
            var initial = root.ToHtmlString();
            Assert.Equal(0, js.Reads);
            if (scenario == "Empty")
            {
                Assert.Contains("disabled", initial);
                Assert.Contains("No published readable chapters", initial);
            }
            else
            {
                var target = scenario == "Single" ? id : meta.DefaultReadableWorkId!.Value;
                Assert.Contains($"href=\"/read/{target}\"", initial);
                Assert.Contains(scenario == "Single" ? "Read Story" : "Start Reading", initial);
            }
            await activator.Link!.HydrateAsync();
            var hydrated = root.ToHtmlString();
            if (scenario == "Resume")
            {
                Assert.Contains("Resume Reading", hydrated);
                Assert.Contains($"href=\"/read/{second}\"", hydrated);
                Assert.Contains($"href=\"/read/{first}\"", hydrated);
                Assert.Contains("Start from beginning", hydrated);
            }
            else
            {
                Assert.DoesNotContain("Resume Reading", hydrated);
                Assert.Equal(initial, hydrated);
            }
        });
    }

    [Fact]
    public async Task NavigationWhileProgressLoads_DiscardsPreviousStoryProgress()
    {
        var firstStory = Meta(Ulid.NewUlid(), Ulid.NewUlid(), Ulid.NewUlid());
        var nextStory = Meta(Ulid.NewUlid(), Ulid.NewUlid(), Ulid.NewUlid());
        var js = new StorageRuntime();
        var progress = new ReaderProgressService(new BrowserProtectedStorageService(
            new ProtectedLocalStorage(js, new EphemeralDataProtectionProvider())));
        await progress.SetLastReadChapterIdAsync(firstStory.Id.ToString(), firstStory.ReadableChildren.Last().Id.ToString());
        var encrypted = js.Values.Values.Single();
        js.Pending = new TaskCompletionSource<string?>();
        var activator = new LinkActivator();
        await using var services = Services(new MetaReader(firstStory, nextStory), progress, activator);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<StoryReadingLink>(Parameters(firstStory.Id));
            var hydrate = activator.Link!.HydrateAsync();
            await activator.Link.SetParametersAsync(Parameters(nextStory.Id));
            js.Pending.SetResult(encrypted);
            await hydrate;
            var html = root.ToHtmlString();
            Assert.Contains($"href=\"/read/{nextStory.DefaultReadableWorkId}\"", html);
            Assert.DoesNotContain("Resume Reading", html);
            Assert.DoesNotContain(firstStory.ReadableChildren.Last().Id.ToString(), html);
        });
    }

    private static LinkedOfGetPublishedWorkMetaResponse Meta(Ulid id, Ulid first, Ulid second) => new()
    {
        Id = id, StoryId = id, WorkType = "Story", DefaultReadableWorkId = first,
        ReadableChildren = [new ReadableWorkItem { Id = first }, new ReadableWorkItem { Id = second }],
        Authors = [], Links = []
    };

    private static ParameterView Parameters(Ulid id) => ParameterView.FromDictionary(
        new Dictionary<string, object?> { [nameof(StoryReadingLink.StoryId)] = id.ToString() });

    private static ServiceProvider Services(IPublishedWorkReader reader, ReaderProgressService progress, LinkActivator activator) =>
        Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder().Services
            .AddSingleton(reader).AddSingleton(progress).AddSingleton<IComponentActivator>(activator)
            .AddRazorComponents().Services.BuildServiceProvider();

    private sealed class TestLink : StoryReadingLink
    {
        public Task HydrateAsync() => OnAfterRenderAsync(false);
    }
    private sealed class LinkActivator : IComponentActivator
    {
        public TestLink? Link { get; private set; }
        public IComponent CreateInstance(Type componentType) => Link = new TestLink();
    }
    private sealed class MetaReader(params LinkedOfGetPublishedWorkMetaResponse[] metas) : IPublishedWorkReader
    {
        public ValueTask<Result<LinkedOfGetPublishedWorkMetaResponse>> GetPublishedWorkMetaAsync(
            Ulid id, string? fields = null, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Result.Success(metas.Single(meta => meta.Id == id)));
        public ValueTask<Result<LinkedOfGetPublishedWorkContentResponse>> GetPublishedWorkContentAsync(
            Ulid id, string? fields = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Reading links must not fetch bodies.");
    }
    private sealed class StorageRuntime : IJSRuntime
    {
        public Dictionary<string, string?> Values { get; } = [];
        public int Reads { get; private set; }
        public bool Unavailable { get; set; }
        public TaskCompletionSource<string?>? Pending { get; set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            var key = (string)args![0]!;
            if (identifier == "localStorage.setItem") Values[key] = (string?)args[1];
            if (identifier == "localStorage.removeItem") Values.Remove(key);
            if (identifier == "localStorage.getItem")
            {
                Reads++;
                if (Unavailable) throw new JSException("Storage unavailable");
                var value = Pending is null ? Values.GetValueOrDefault(key) : await Pending.Task;
                return (TValue)(object)value!;
            }
            return default!;
        }
    }
}
