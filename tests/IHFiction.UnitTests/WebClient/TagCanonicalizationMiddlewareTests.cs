using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Services;
using IHFiction.WebClient.Tags;

namespace IHFiction.UnitTests.WebClient;

public sealed class TagCanonicalizationMiddlewareTests
{
    [Theory]
    [InlineData("NotFound", 404, "text/html")]
    [InlineData("InvalidSpec", 400, "text/html")]
    [InlineData("Conflict", 409, "text/markdown")]
    public async Task Failure_ReturnsStatusAndUnavailableRepresentation(string suffix, int status, string accept)
    {
        using var handler = new ResponseHandler(status,
            $"{{\"status\":{status},\"domainError\":{{\"code\":\"ResolveTagLanding.{suffix}\",\"description\":\"Use /tags/genre:drama\"}}}}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example/") };
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/tags/drama";
        context.Request.Headers.Accept = accept;
        context.Response.Body = new MemoryStream();
        var invoked = false;
        var middleware = new TagCanonicalizationMiddleware(_ => { invoked = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(context, new TagService(new FictionApiClient(http)), new TagLandingRequestContext());
        Assert.False(invoked);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.StartsWith(accept, context.Response.ContentType);
        Assert.Equal("noindex", context.Response.Headers["X-Robots-Tag"]);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        Assert.Contains("Tag page unavailable", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public async Task Alias_PermanentlyRedirectsAndPreservesQuery(string method)
    {
        using var handler = new ResponseHandler(200, "{\"canonicalSpec\":\"genre:drama\",\"tags\":[],\"storyCount\":1}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example/") };
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/tags/Drama";
        context.Request.QueryString = new QueryString("?page=2");
        var middleware = new TagCanonicalizationMiddleware(_ => throw new InvalidOperationException("Must redirect before rendering."));
        await middleware.InvokeAsync(context, new TagService(new FictionApiClient(http)), new TagLandingRequestContext());
        Assert.Equal(301, context.Response.StatusCode);
        Assert.Equal("/tags/genre:drama?page=2", context.Response.Headers.Location);
    }

    private sealed class ResponseHandler(int status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/problem+json")
            });
    }
}
