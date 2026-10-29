using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using IHFiction.WebClient.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace IHFiction.UnitTests.WebClient;

public sealed class VisitorPersistenceTests
{
    [Fact]
    public async Task RegisteredVisitor_IsRestoredIntoCircuitScope_WithoutAComponentResolvingIt()
    {
        var builder = WebApplication.CreateBuilder();
        var instancesCreated = 0;
        builder.Services.AddScoped(_ => { instancesCreated++; return new VisitorContext(); });
        builder.Services.AddRazorComponents().RegisterPersistentService<VisitorContext>(RenderMode.InteractiveServer);
        await using var app = builder.Build();
        var store = new StateStore();
        await using var request = app.Services.CreateAsyncScope();
        var captured = request.ServiceProvider.GetRequiredService<VisitorContext>();
        captured.ClientIp = "203.0.113.7";
        var prerender = new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, request.ServiceProvider);
        await prerender.RestoreStateAsync(store);
        await using (var renderer = new PersistenceRenderer(request.ServiceProvider))
            await prerender.PersistStateAsync(store, renderer);

        await using var circuit = app.Services.CreateAsyncScope();
        var interactive = new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, circuit.ServiceProvider);
        await interactive.RestoreStateAsync(store);
        // No component or caller has requested the new scoped instance yet.
        instancesCreated.Should().Be(2);
        var restored = circuit.ServiceProvider.GetRequiredService<VisitorContext>();
        restored.Should().NotBeSameAs(captured);
        restored.ClientIp.Should().Be("203.0.113.7");

        await using var otherCircuit = app.Services.CreateAsyncScope();
        var other = new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, otherCircuit.ServiceProvider);
        await other.RestoreStateAsync(new StateStore());
        otherCircuit.ServiceProvider.GetRequiredService<VisitorContext>().ClientIp.Should().BeNull();
    }

    private sealed class StateStore : IPersistentComponentStateStore
    {
        private Dictionary<string, byte[]> state = [];
        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() => Task.FromResult<IDictionary<string, byte[]>>(new Dictionary<string, byte[]>(state));
        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> data)
        {
            state = new Dictionary<string, byte[]>(data);
            return Task.CompletedTask;
        }
        public bool SupportsRenderMode(IComponentRenderMode mode) => mode is InteractiveServerRenderMode;
    }

    [SuppressMessage("Usage", "BL0006:Do not use RenderTree types", Justification = "The framework persistence API requires a renderer. This fixture only supplies its dispatcher; no components or render-tree internals are exercised.")]
    private sealed class PersistenceRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();
        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
        protected override void HandleException(Exception exception) => System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
    }
}
