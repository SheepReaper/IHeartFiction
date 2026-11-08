using IHFiction.SharedWeb.Components;
using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Services;
using IHFiction.WebClient.Features.AgentAccess;
using IHFiction.WebClient.Features.Reading;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Components.Sections;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IHFiction.UnitTests.Features.Reading;

public sealed class MarkdownResponseHttpTests
{
    [Theory]
    [InlineData("text/html", "Accept-Encoding", "Accept-Encoding, Accept")]
    [InlineData("text/markdown", "Accept-Encoding", "Accept-Encoding, Accept")]
    [InlineData("text/html", "accept, Accept-Encoding, Accept", "accept, Accept-Encoding")]
    [InlineData("text/markdown", "*", "*")]
    [InlineData("text/html", "*", "*")]
    public async Task ComponentGet_MergesVaryBeforeResponseStarts(string accept, string downstreamVary, string expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["AllowedHosts"] = "*";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<HtmlToMarkdownConverter>();
        await using var app = builder.Build();
        app.UseRouting();
        app.UseMiddleware<MarkdownResponseMiddleware>();
        app.MapMethods("/page", ["GET", "POST"], async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.Vary = downstreamVary;
            await context.Response.WriteAsync("<main data-agent-content><h1>Hello</h1></main>");
        }).WithMetadata(new ComponentTypeMetadata(typeof(TestComponent)));
        app.MapGet("/plain", () => "plain");
        await app.StartAsync(TestContext.Current.CancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        using var response = await client.GetAsync("/page", TestContext.Current.CancellationToken);
        Assert.Equal(expected, string.Join(", ", response.Headers.Vary));
        var markdown = accept == "text/markdown";
        Assert.Equal(markdown ? "text/markdown" : "text/html", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(markdown ? "# Hello\n" : "<main data-agent-content><h1>Hello</h1></main>", body);
        using var plain = await client.GetAsync("/plain", TestContext.Current.CancellationToken);
        Assert.Empty(plain.Headers.Vary);
        using var post = await client.PostAsync("/page", null, TestContext.Current.CancellationToken);
        Assert.Equal(downstreamVary, string.Join(", ", post.Headers.Vary));
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, "/page");
        using var head = await client.SendAsync(headRequest, TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.MethodNotAllowed, head.StatusCode);
        Assert.Empty(head.Headers.Vary);
    }

    [Theory]
    [InlineData("https://example.test/stories/01J00000000000000000000000", null, true)]
    [InlineData("https://example.test/read/01J00000000000000000000000", "/read/01J00000000000000000000000", true)]
    [InlineData("https://example.test/authors/01J00000000000000000000000", null, true)]
    [InlineData("https://example.test/read/01J00000000000000000000000", null, false)]
    public async Task SocialPreviewMetadata_EmitsMarkdownAlternateLinkWhenEmitCanonicalIsTrue(
        string currentUri,
        string? canonicalUrl,
        bool emitCanonical)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorComponents();
        builder.Services.AddSingleton<NavigationManager>(new TestNavigation(currentUri));
        builder.Services.Configure<SiteUrlOptions>(options => options.BaseUrl = new Uri("https://example.test"));
        builder.Services.AddSingleton<MetadataUrlService>();

        await using var provider = builder.Services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<MetadataHost>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(MetadataHost.CanonicalUrl)] = canonicalUrl,
                    [nameof(MetadataHost.EmitCanonical)] = emitCanonical
                }));
            return component.ToHtmlString();
        });

        if (emitCanonical)
        {
            Assert.Contains($"<link rel=\"canonical\" href=\"{currentUri}\" />", html);
            Assert.Contains($"<link rel=\"alternate\" type=\"text/markdown\" href=\"{currentUri}\" />", html);
        }
        else
        {
            Assert.DoesNotContain("rel=\"canonical\"", html);
            Assert.DoesNotContain("type=\"text/markdown\"", html);
        }
    }

    [Fact]
    public async Task FollowingAdvertisedMarkdownLink_ReturnsNegotiatedMarkdownWithVaryAccept()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["AllowedHosts"] = "*";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<HtmlToMarkdownConverter>();
        builder.Services.Configure<ApiUrlOptions>(options => options.BaseUrl = new Uri("https://api.example.test"));

        await using var app = builder.Build();
        app.UseRouting();
        app.UseMiddleware<MarkdownResponseMiddleware>();
        app.UseMiddleware<HomepageLinkHeaderMiddleware>();
        app.MapGet("/read/01J00000000000000000000000", async context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(
                "<head><link rel=\"canonical\" href=\"/read/01J00000000000000000000000\" /><link rel=\"alternate\" type=\"text/markdown\" href=\"/read/01J00000000000000000000000\" /></head><main data-agent-content><h1>Chapter 1</h1><p>Full prose paragraph.</p></main>");
        }).WithMetadata(new ComponentTypeMetadata(typeof(TestComponent)));

        await app.StartAsync(TestContext.Current.CancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        // 1. Fetch HTML page and discover Markdown alternate link in both HTTP Link header and HTML <head>
        using var htmlRequest = new HttpRequestMessage(HttpMethod.Get, "/read/01J00000000000000000000000");
        htmlRequest.Headers.Accept.ParseAdd("text/html");
        using var htmlResponse = await client.SendAsync(htmlRequest, TestContext.Current.CancellationToken);
        Assert.Equal("text/html", htmlResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Accept", htmlResponse.Headers.Vary, StringComparer.OrdinalIgnoreCase);

        var linkHeaders = htmlResponse.Headers.GetValues("Link").ToArray();
        Assert.Contains(linkHeaders, l => l.Contains("</read/01J00000000000000000000000>; rel=\"alternate\"; type=\"text/markdown\"", StringComparison.Ordinal));

        var htmlBody = await htmlResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("<link rel=\"alternate\" type=\"text/markdown\" href=\"/read/01J00000000000000000000000\" />", htmlBody);

        // 2. Follow advertised representation with Accept: text/markdown
        using var mdRequest = new HttpRequestMessage(HttpMethod.Get, "/read/01J00000000000000000000000");
        mdRequest.Headers.Accept.ParseAdd("text/markdown");
        using var mdResponse = await client.SendAsync(mdRequest, TestContext.Current.CancellationToken);
        Assert.Equal("text/markdown", mdResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Accept", mdResponse.Headers.Vary, StringComparer.OrdinalIgnoreCase);

        var mdBody = await mdResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("# Chapter 1", mdBody);
        Assert.Contains("Full prose paragraph.", mdBody);
    }

    private sealed class TestComponent : ComponentBase;

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation(string uri) => Initialize("https://example.test/", uri);
    }

    private sealed class MetadataHost : ComponentBase
    {
        [Parameter] public string? CanonicalUrl { get; set; }
        [Parameter] public bool EmitCanonical { get; set; } = true;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenComponent<SectionOutlet>(0);
            builder.AddComponentParameter(1, nameof(SectionOutlet.SectionName), "app-head-metadata");
            builder.CloseComponent();

            builder.OpenComponent<SocialPreviewMetadata>(2);
            builder.AddComponentParameter(3, nameof(SocialPreviewMetadata.Title), "Test Title");
            builder.AddComponentParameter(4, nameof(SocialPreviewMetadata.Description), "Test Description");
            builder.AddComponentParameter(5, nameof(SocialPreviewMetadata.CanonicalUrl), CanonicalUrl);
            builder.AddComponentParameter(6, nameof(SocialPreviewMetadata.EmitCanonical), EmitCanonical);
            builder.CloseComponent();
        }
    }
}
