using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.FictionApi.Stories;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Pagination;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for ListStoryChapters functionality
/// Tests request validation, response model construction, and authorization logic
/// </summary>
public class ListStoryChaptersTests
{
    [Fact]
    public void CollectionResponse_WithMultipleChapters_CanBeCreated()
    {
        // Arrange
        var chapters = new List<ListPublishedStoryChapters.ListPublishedStoryChaptersItem>
        {
            new(Ulid.NewUlid(), "Chapter 1", 0, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, true, 1000),
            new(Ulid.NewUlid(), "Chapter 2", 1, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, false, 0) // Published chapters only
        };

        // Act
        var response = new CollectionResponse<ListPublishedStoryChapters.ListPublishedStoryChaptersItem>(chapters.AsQueryable());

        // Assert
        Assert.Equal(2, response.Data.Count());
        Assert.Equal("Chapter 1", response.Data.First().Title);
        Assert.Equal("Chapter 2", response.Data.Last().Title);
    }

    [Fact]
    public void CollectionResponse_WithSingleChapter_CanBeCreated()
    {
        // Arrange
        var chapters = new List<ListPublishedStoryChapters.ListPublishedStoryChaptersItem>
        {
            new(Ulid.NewUlid(), "Chapter 1", 0, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, true, 1000)
        };

        // Act
        var response = new CollectionResponse<ListPublishedStoryChapters.ListPublishedStoryChaptersItem>(chapters.AsQueryable());

        // Assert
        Assert.Single(response.Data);
        Assert.Equal("Chapter 1", response.Data.First().Title);
    }

    [Fact]
    public void WithLinks_ForStoryChapters_IncludesStoryIdRouteValue()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var chapter = new ListPublishedStoryChapters.ListPublishedStoryChaptersItem(
            Ulid.NewUlid(), "Chapter 1", 0, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, true, 1000);
        var paged = new PagedCollection<ListPublishedStoryChapters.ListPublishedStoryChaptersItem>(
            new[] { chapter }.AsQueryable(),
            1,
            1,
            10);
        var linker = CreateLinkService();

        // Act
        var linked = paged.WithLinks(
            linker,
            ListPublishedStoryChapters.EndpointName,
            item => new Linked<ListPublishedStoryChapters.ListPublishedStoryChaptersItem>(item, Enumerable.Empty<LinkItem>()),
            new ListPublishedStoryChapters.ListPublishedStoryChaptersQuery(),
            routeValues: [new KeyValuePair<string, string?>("id", storyId.ToString())]);

        // Assert
        var selfLink = Assert.Single(linked.Links);
        Assert.Equal("self", selfLink.Rel);
        Assert.Equal($"https://example.test/stories/{storyId}/chapters?page=1&pageSize=10", selfLink.Href);
    }

    private static LinkService CreateLinkService()
    {
        var linkGenerator = new StoryChaptersLinkGenerator();
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

        return new LinkService(linkGenerator, httpContextAccessor);
    }

    private sealed class StoryChaptersLinkGenerator : LinkGenerator
    {
        public override string? GetPathByAddress<TAddress>(
            TAddress address,
            RouteValueDictionary values,
            PathString pathBase = default,
            FragmentString fragment = default,
            LinkOptions? options = null)
            => GetPathByAddress(
                httpContext: null,
                address,
                values,
                ambientValues: null,
                pathBase,
                fragment,
                options);

        public override string? GetPathByAddress<TAddress>(
            HttpContext? httpContext,
            TAddress address,
            RouteValueDictionary values,
            RouteValueDictionary? ambientValues = null,
            PathString? pathBase = null,
            FragmentString fragment = default,
            LinkOptions? options = null)
            => null;

        public override string? GetUriByAddress<TAddress>(
            TAddress address,
            RouteValueDictionary values,
            string scheme,
            HostString host,
            PathString pathBase = default,
            FragmentString fragment = default,
            LinkOptions? options = null)
            => GetUriByAddress(
                httpContext: null,
                address,
                values,
                ambientValues: null,
                scheme,
                host,
                pathBase,
                fragment,
                options);

        public override string? GetUriByAddress<TAddress>(
            HttpContext? httpContext,
            TAddress address,
            RouteValueDictionary values,
            RouteValueDictionary? ambientValues = null,
            string? scheme = null,
            HostString? host = null,
            PathString? pathBase = null,
            FragmentString fragment = default,
            LinkOptions? options = null)
        {
            if (!values.TryGetValue("id", out var id) || string.IsNullOrWhiteSpace(id?.ToString()))
                return null;

            var page = values[nameof(IPaginationSupport.Page)];
            var pageSize = values[nameof(IPaginationSupport.PageSize)];

            return $"https://example.test/stories/{id}/chapters?page={page}&pageSize={pageSize}";
        }
    }
}
