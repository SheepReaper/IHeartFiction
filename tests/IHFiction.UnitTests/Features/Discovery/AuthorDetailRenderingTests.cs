using System.Net;
using System.Text.Json;

using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Components;
using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Features.Discovery;
using IHFiction.SharedWeb.Services;

using Markdig;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IHFiction.UnitTests.Features.Discovery;

public sealed class AuthorDetailRenderingTests
{
    [Theory]
    [InlineData("## Reader\n\nA **bold** [biography](https://example.test).")]
    [InlineData("")]
    [InlineData(" \n ")]
    [InlineData(null)]
    public async Task Profile_RendersCreationDateAndPlainMetadataWithoutEditingControls(string? bio)
    {
        var activator = new MetadataCapture();
        using var handler = new ProfileResponse(bio);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorComponents();
        builder.Services.AddSingleton<IComponentActivator>(activator);
        builder.Services.AddSingleton<NavigationManager, TestNavigation>();
        builder.Services.Configure<SiteUrlOptions>(options => options.BaseUrl = new Uri("https://example.test"));
        builder.Services.AddSingleton<MetadataUrlService>();
        builder.Services.AddSingleton(new MarkdownPipelineBuilder().Build());
        builder.Services.AddSingleton(new AuthorService(new FictionApiClient(client)));
        await using var provider = builder.Services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<AuthorDetail>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(AuthorDetail.AuthorId)] = Ulid.NewUlid().ToString() }));
            return component.ToHtmlString();
        });
        Assert.Contains("Joined Sep 2025", html);
        Assert.DoesNotContain("Joined May 2026", html);
        Assert.DoesNotContain("href=\"/account/settings\"", html);
        Assert.DoesNotContain("Edit profile", html);
        var description = Assert.IsType<string>(activator.Description);
        using var schemas = JsonDocument.Parse(JsonSerializer.Serialize(activator.Schemas));
        var person = schemas.RootElement[0].GetProperty("mainEntity");
        if (string.IsNullOrWhiteSpace(bio))
        {
            Assert.Contains("Read stories by", description);
            Assert.Equal(JsonValueKind.Null, person.GetProperty("description").ValueKind);
        }
        else
        {
            Assert.Contains("<strong>bold</strong>", html);
            Assert.Contains("biography", description);
            Assert.DoesNotContain("##", description);
            Assert.DoesNotContain("**", description);
            Assert.DoesNotContain("[biography]", description);
            Assert.Equal(description, person.GetProperty("description").GetString());
        }
    }

    private sealed class ProfileResponse(string? bio) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    userId = Guid.NewGuid(), name = "Reader", createdAt = "2025-09-28T00:00:00Z", updatedAt = "2026-05-06T00:00:00Z",
                    profile = new { bio, socialLinks = Array.Empty<object>() }, publishedStories = Array.Empty<object>(), totalStories = 0,
                    links = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            });
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://example.test/", "https://example.test/authors/test");
    }

    private sealed class MetadataCapture : IComponentActivator
    {
        public object? Description { get; set; }
        public object? Schemas { get; set; }
        public IComponent CreateInstance(Type componentType)
            => componentType == typeof(AuthorDetail) ? new AuthorDetail() : new ParameterSink(this, componentType);
    }

    private sealed class ParameterSink(MetadataCapture capture, Type componentType) : IComponent
    {
        public void Attach(RenderHandle renderHandle) { }
        public Task SetParametersAsync(ParameterView parameters)
        {
            if (componentType == typeof(SocialPreviewMetadata)) capture.Description = parameters.GetValueOrDefault<object>("Description");
            if (componentType == typeof(StructuredDataMetadata)) capture.Schemas = parameters.GetValueOrDefault<object>("Schemas");
            return Task.CompletedTask;
        }
    }
}
