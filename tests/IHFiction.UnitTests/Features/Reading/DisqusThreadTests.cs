using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Features.Reading;
using IHFiction.SharedWeb.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace IHFiction.UnitTests.Features.Reading;

public sealed class DisqusThreadTests
{
    [Fact]
    public async Task ReusedComponent_UpdatesChangedThreadsWithoutReloadingUnchangedThread()
    {
        var js = new DisqusRuntime();
        var activator = new ThreadActivator();
        await using var services = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder().Services
            .AddSingleton<IJSRuntime>(js)
            .AddSingleton(Options.Create(new DisqusOptions { ShortName = "test-forum" }))
            .AddSingleton(new MetadataUrlService(Options.Create(new SiteUrlOptions { BaseUrl = new Uri("https://example.com") })))
            .AddSingleton<NavigationManager>(new TestNavigation())
            .AddSingleton<IComponentActivator>(activator)
            .AddRazorComponents().Services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<DisqusThread>(Parameters("one", "/read/one"));
            var component = activator.Component!;
            await component.HydrateAsync();
            await component.HydrateAsync();
            Assert.Single(js.Threads);
            await component.SetParametersAsync(Parameters("two", "/read/two"));
            await component.HydrateAsync();
            await component.SetParametersAsync(Parameters("two", "/read/canonical-two"));
            await component.HydrateAsync();
            Assert.Equal(1, js.Imports);
            Assert.Equal(new[] { ("https://example.com/read/one", "one"),
                ("https://example.com/read/two", "two"),
                ("https://example.com/read/canonical-two", "two") }, js.Threads);
        });
    }

    private static ParameterView Parameters(string id, string url) => ParameterView.FromDictionary(
        new Dictionary<string, object?> { [nameof(DisqusThread.PageIdentifier)] = id, [nameof(DisqusThread.PageUrl)] = url });

    private sealed class TestThread : DisqusThread
    {
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public TestThread() => PageIdentifier = string.Empty;

        public Task HydrateAsync() => OnAfterRenderAsync(false);
    }

    private sealed class ThreadActivator : IComponentActivator
    {
        public TestThread? Component { get; private set; }
        public IComponent CreateInstance(Type componentType) => Component = new TestThread();
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://example.com/", "https://example.com/read/one");
    }

    private sealed class DisqusRuntime : IJSRuntime, IJSObjectReference
    {
        public int Imports { get; private set; }
        public List<(string Url, string Identifier)> Threads { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import")
            {
                Imports++;
                return ValueTask.FromResult((TValue)(object)this);
            }
            Assert.Equal("loadDisqus", identifier);
            Threads.Add(((string)args![0]!, (string)args[1]!));
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
