using System.Text;

using FluentAssertions;

using IHFiction.WebClient.MarkdownResponses;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http;

namespace IHFiction.UnitTests.WebClient;

public sealed class MarkdownResponseMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ForNegotiatedComponentResponse_ReturnsMarkdownHeadersAndBody()
    {
        var context = CreateContext(HttpMethods.Get, "text/markdown", isComponent: true);
        context.Response.Headers.Vary = "Accept-Encoding";
        var sut = CreateMiddleware("<html><body><main data-agent-content><h1>Hello</h1></main></body></html>");

        await sut.InvokeAsync(context, new HtmlToMarkdownConverter());

        context.Response.ContentType.Should().Be("text/markdown; charset=utf-8");
        context.Response.Headers.Vary.ToString().Should().Be("Accept-Encoding, Accept");
        context.Response.Headers["Content-Signal"].ToString().Should().Be("ai-train=no, search=yes, ai-input=yes");
        ReadBody(context).Should().Be("# Hello\n");
    }

    [Fact]
    public async Task InvokeAsync_WhenVaryAlreadyContainsAccept_DoesNotDuplicateIt()
    {
        var context = CreateContext(HttpMethods.Get, "text/markdown", isComponent: true);
        context.Response.Headers.Vary = "accept, Accept-Encoding";
        var sut = CreateMiddleware("<main data-agent-content><p>Hello</p></main>");

        await sut.InvokeAsync(context, new HtmlToMarkdownConverter());

        context.Response.Headers.Vary.ToString().Should().Be("accept, Accept-Encoding");
    }

    [Fact]
    public async Task InvokeAsync_ForNonComponentEndpoint_PreservesHtmlResponse()
    {
        var context = CreateContext(HttpMethods.Get, "text/markdown", isComponent: false);
        var sut = CreateMiddleware("<main data-agent-content><h1>Hello</h1></main>");

        await sut.InvokeAsync(context, new HtmlToMarkdownConverter());

        context.Response.ContentType.Should().Be("text/html; charset=utf-8");
        context.Response.Headers.Should().NotContainKey("Content-Signal");
        ReadBody(context).Should().Contain("<h1>Hello</h1>");
    }

    [Theory]
    [InlineData(StatusCodes.Status404NotFound, "text/html")]
    [InlineData(StatusCodes.Status200OK, "application/json")]
    public async Task InvokeAsync_ForIneligibleDownstreamResponse_PreservesResponse(int statusCode, string contentType)
    {
        var context = CreateContext(HttpMethods.Get, "text/markdown", isComponent: true);
        var sut = CreateMiddleware("unchanged", statusCode, contentType);

        await sut.InvokeAsync(context, new HtmlToMarkdownConverter());

        context.Response.StatusCode.Should().Be(statusCode);
        context.Response.ContentType.Should().Be(contentType);
        context.Response.Headers.Should().NotContainKey("Content-Signal");
        ReadBody(context).Should().Be("unchanged");
    }

    private static MarkdownResponseMiddleware CreateMiddleware(
        string responseBody,
        int statusCode = StatusCodes.Status200OK,
        string contentType = "text/html; charset=utf-8") =>
        new(async context =>
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            await context.Response.WriteAsync(responseBody);
        });

    private static DefaultHttpContext CreateContext(string method, string accept, bool isComponent)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Headers.Accept = accept;
        context.Response.Body = new MemoryStream();

        if (isComponent)
        {
            context.SetEndpoint(new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(new ComponentTypeMetadata(typeof(TestComponent))),
                "test component"));
        }

        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private sealed class TestComponent : ComponentBase;
}