using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Services;
using IHFiction.WebClient.Reading;
using Microsoft.AspNetCore.Http;

namespace IHFiction.UnitTests.WebClient;

public sealed class ReaderRedirectMiddlewareTests
{
    [Theory]
    [InlineData("GET", "text/html", "Story")]
    [InlineData("GET", "text/markdown", "Story")]
    [InlineData("GET", "text/html", "Book")]
    [InlineData("GET", "text/markdown", "Book")]
    [InlineData("HEAD", "text/html", "Book")]
    public async Task Container_RedirectsBeforeRenderingOrFetchingContent(string method, string accept, string type)
    {
        var id = Ulid.NewUlid();
        var storyId = type == "Story" ? id : Ulid.NewUlid();
        var meta = new LinkedOfGetPublishedWorkMetaResponse
        {
            Id = id, StoryId = storyId, WorkType = type, IsDirectlyReadable = false,
            Authors = [], ReadableChildren = [], Links = []
        };
        var context = Context(id, method, accept);
        var middleware = new ReaderRedirectMiddleware(_ => throw new InvalidOperationException("Must not render."));
        await middleware.InvokeAsync(context, new FakeReader(Result.Success(meta)));
        Assert.Equal(303, context.Response.StatusCode);
        Assert.Equal($"/stories/{storyId}" + (type == "Book" ? $"#book-{id}" : ""), context.Response.Headers.Location);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Theory]
    [InlineData("Story")]
    [InlineData("Chapter")]
    public async Task DirectWork_ReusesMetadataWithoutRedirect(string type)
    {
        var id = Ulid.NewUlid();
        var meta = new LinkedOfGetPublishedWorkMetaResponse { Id = id, WorkType = type, IsDirectlyReadable = true };
        var context = Context(id);
        var invoked = false;
        await new ReaderRedirectMiddleware(_ => { invoked = true; return Task.CompletedTask; })
            .InvokeAsync(context, new FakeReader(Result.Success(meta)));
        Assert.True(invoked);
        Assert.Empty(context.Response.Headers.Location.ToString());
        Assert.Same(meta, context.Items[typeof(LinkedOfGetPublishedWorkMetaResponse)]);
    }

    [Theory]
    [InlineData("Work.NotFound")]
    [InlineData("Story.NotPublished")]
    [InlineData("Book.NotPublished")]
    public async Task UnavailableWork_DoesNotRedirectOrExposeMetadata(string code)
    {
        var context = Context(Ulid.NewUlid());
        var invoked = false;
        await new ReaderRedirectMiddleware(_ => { invoked = true; return Task.CompletedTask; })
            .InvokeAsync(context, new FakeReader(Result.Failure<LinkedOfGetPublishedWorkMetaResponse>(new DomainError(code, "Unavailable"))));
        Assert.True(invoked);
        Assert.Empty(context.Response.Headers.Location.ToString());
        Assert.Empty(context.Items);
    }

    private static DefaultHttpContext Context(Ulid id, string method = "GET", string accept = "text/html")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = $"/read/{id}";
        context.Request.Headers.Accept = accept;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class FakeReader(Result<LinkedOfGetPublishedWorkMetaResponse> result) : IPublishedWorkReader
    {
        public ValueTask<Result<LinkedOfGetPublishedWorkMetaResponse>> GetPublishedWorkMetaAsync(
            Ulid id, string? fields = null, CancellationToken cancellationToken = default) => ValueTask.FromResult(result);
        public ValueTask<Result<LinkedOfGetPublishedWorkContentResponse>> GetPublishedWorkContentAsync(
            Ulid id, string? fields = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Routing must not fetch a body.");
    }
}
