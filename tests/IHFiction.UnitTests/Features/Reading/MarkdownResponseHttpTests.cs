using IHFiction.WebClient.Features.Reading;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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

    private sealed class TestComponent : ComponentBase;
}
