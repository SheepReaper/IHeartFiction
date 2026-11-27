using System.Security.Claims;
using System.Reflection;

using IHFiction.SharedKernel.Notifications;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Features.AccountManagement;
using IHFiction.SharedWeb.Features.Following;
using IHFiction.SharedWeb.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace IHFiction.UnitTests.SharedWeb;

internal sealed class AuthenticationComponentHost : IAsyncDisposable
{
    private readonly ServiceProvider services;
    private readonly Activator activator = new();
    private readonly RenderFragment child;
    public HtmlRenderer Renderer { get; }
    public HtmlRootComponent Root { get; private set; }
    public LoaderService Loader => services.GetRequiredService<LoaderService>();
    public Task ParametersChanged => activator.Component!.ParametersChanged;

    public AuthenticationComponentHost(Type componentType, FictionApiClient client, Dictionary<string, object>? parameters = null,
        Func<Task<BrowserPushSubscription?>>? requestSubscription = null)
    {
        child = builder =>
        {
            builder.OpenComponent(0, componentType);
            if (parameters is not null) builder.AddMultipleAttributes(1, parameters);
            builder.CloseComponent();
        };
        services = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder().Services
            .AddSingleton(client)
            .AddSingleton(new NotificationService(client, () => Task.FromResult<string?>("test-device")))
            .AddSingleton(new AccountService(client))
            .AddSingleton<LoaderService>()
            .AddSingleton<IJSRuntime>(new ModuleRuntime(requestSubscription ?? (() => Task.FromResult<BrowserPushSubscription?>(null))))
            .AddSingleton(Options.Create(new WebPushOptions { PublicKey = "test", PrivateKey = "test", Subject = "mailto:test@example.test" }))
            .AddSingleton<NavigationManager, Navigation>()
            .AddSingleton<AntiforgeryStateProvider, TokenState>()
            .AddSingleton<IComponentActivator>(activator)
            .AddAuthorizationCore()
            .AddRazorComponents().Services.BuildServiceProvider();
        Renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
    }

    public static Task<AuthenticationState> State(string? user) => Task.FromResult(new AuthenticationState(
        new ClaimsPrincipal(user is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.Name, user)], "test"))));

    public async Task RenderAsync(Task<AuthenticationState> state) =>
        Root = await Renderer.RenderComponentAsync<CascadingValue<Task<AuthenticationState>>>(Parameters(state));

    public Task ChangeAuthenticationAsync(Task<AuthenticationState> state) => activator.Cascade!.SetParametersAsync(Parameters(state));
    public Task HydrateAsync(bool firstRender = true) => activator.Component!.HydrateAsync(firstRender);

    // HtmlRenderer does not dispatch interactive DOM events. Invoke the bound handler,
    // then use normal parameter rendering to inspect its final UI state.
    public Task InvokeActionAsync(string name, params object[] args) => (Task)activator.Component!.GetType().BaseType!
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(activator.Component, args)!;

    public Task RenderCurrentAsync() => ((ComponentBase)activator.Component!).SetParametersAsync(ParameterView.Empty);

    private ParameterView Parameters(Task<AuthenticationState> state) => ParameterView.FromDictionary(new Dictionary<string, object?>
    {
        [nameof(CascadingValue<>.Value)] = state,
        [nameof(CascadingValue<>.ChildContent)] = child
    });

    public async ValueTask DisposeAsync()
    {
        await Renderer.DisposeAsync();
        await services.DisposeAsync();
    }

    private interface ITestComponent
    {
        Task HydrateAsync(bool firstRender);
        Task ParametersChanged { get; }
    }

    private sealed class TestAuthor : AuthorFollowButton, ITestComponent
    {
        public Task ParametersChanged { get; private set; } = Task.CompletedTask;
        protected override Task OnParametersSetAsync() => ParametersChanged = base.OnParametersSetAsync();
        public Task HydrateAsync(bool firstRender) => OnAfterRenderAsync(firstRender);
    }

    private sealed class TestStory : StoryFollowButton, ITestComponent
    {
        public Task ParametersChanged { get; private set; } = Task.CompletedTask;
        protected override Task OnParametersSetAsync() => ParametersChanged = base.OnParametersSetAsync();
        public Task HydrateAsync(bool firstRender) => OnAfterRenderAsync(firstRender);
    }

    private sealed class TestFollowing : Following, ITestComponent
    {
        public Task ParametersChanged { get; private set; } = Task.CompletedTask;
        protected override Task OnParametersSetAsync() => ParametersChanged = base.OnParametersSetAsync();
        public Task HydrateAsync(bool firstRender) => OnAfterRenderAsync(firstRender);
    }

    private sealed class TestLogin : LoginDisplay, ITestComponent
    {
        public Task ParametersChanged { get; private set; } = Task.CompletedTask;
        protected override Task OnParametersSetAsync() => ParametersChanged = base.OnParametersSetAsync();
        public Task HydrateAsync(bool firstRender) => OnAfterRenderAsync(firstRender);
    }

    private sealed class Activator : IComponentActivator
    {
        public CascadingValue<Task<AuthenticationState>>? Cascade { get; private set; }
        public ITestComponent? Component { get; private set; }
        public IComponent CreateInstance(Type type)
        {
            IComponent component = type == typeof(AuthorFollowButton) ? new TestAuthor()
                : type == typeof(StoryFollowButton) ? new TestStory()
                : type == typeof(Following) ? new TestFollowing()
                : type == typeof(LoginDisplay) ? new TestLogin()
                : (IComponent)System.Activator.CreateInstance(type)!;
            if (component is CascadingValue<Task<AuthenticationState>> cascade) Cascade = cascade;
            if (component is ITestComponent testComponent) Component = testComponent;
            return component;
        }
    }

    private class ModuleState(Func<Task<BrowserPushSubscription?>> requestSubscription)
    {
        protected async Task<TValue> SubscriptionAsync<TValue>() => (TValue)(object)(await requestSubscription())!;
    }

    private sealed class ModuleRuntime(Func<Task<BrowserPushSubscription?>> requestSubscription)
        : ModuleState(requestSubscription), IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            identifier == "import" ? new((TValue)(object)this) : new(SubscriptionAsync<TValue>());
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            identifier == "import" ? new((TValue)(object)this) : new(SubscriptionAsync<TValue>());
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("https://example.test/", "https://example.test/");
    }

    private sealed class TokenState : AntiforgeryStateProvider
    {
        public override AntiforgeryRequestToken GetAntiforgeryToken() => new("test-token", "__RequestVerificationToken");
    }
}
