using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.FictionApi.Stories;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Pagination;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for ListPublishedStories functionality
/// Tests request validation, response model construction, and query parameter handling
/// </summary>
public class ListPublishedStoriesTests
{
    // Note: Request validation tests removed as they test low-value implementation details.
    // Validation is handled automatically by .NET 10's AddValidation() system with
    // business logic validation in the endpoint implementation.

    [Fact]
    public void PublishedStoryItem_CanBeCreated()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var publishedAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow.AddMinutes(1);

        // Act
        var item = new ListPublishedStories.ListPublishedStoriesItem(
            storyId,
            "Test Story",
            "A test story description",
            publishedAt,
            updatedAt,
            true,  // HasContent
            false, // HasChapters
            false, // HasBooks
            false, // HasCoverImage
            0,
            Ulid.NewUlid(),
            "Author Name");

        // Assert
        Assert.Equal(storyId, item.StoryId);
        Assert.Equal("Test Story", item.Title);
        Assert.Equal("A test story description", item.Description);
        Assert.Equal(publishedAt, item.PublishedAt);
        Assert.Equal(updatedAt, item.UpdatedAt);
        Assert.Equal("Author Name", item.AuthorName);
        Assert.True(item.HasContent);
        Assert.False(item.HasChapters);
        Assert.False(item.HasBooks);
        Assert.Equal(0, item.ChapterCount);
    }

    [Fact]
    public void PaginatedCollectionResponse_CanBeCreated()
    {
        // Arrange
        var stories = new List<ListPublishedStories.ListPublishedStoriesItem>
        {
            new(Ulid.NewUlid(), "Story 1", "Description 1", DateTime.UtcNow, DateTime.UtcNow, true, false, false, false, 0, Ulid.NewUlid(), "Author 1"),
            new(Ulid.NewUlid(), "Story 2", "Description 2", DateTime.UtcNow, DateTime.UtcNow, false, true, false, true, 5, Ulid.NewUlid(),"Author 2")
        };

        // Act
        var response = new PagedCollection<ListPublishedStories.ListPublishedStoriesItem>(
            stories.AsQueryable(),
            25,  // TotalCount
            2,   // CurrentPage
            10); // PageSize

        // Assert
        Assert.Equal(2, response.Data.Count());
        Assert.Equal(25, response.TotalCount);
        Assert.Equal(3, response.TotalPages);
        Assert.Equal(2, response.CurrentPage);
        Assert.Equal(10, response.PageSize);
        Assert.True(response.HasNextPage);
        Assert.True(response.HasPreviousPage);
    }

    [Fact]
    public void LinkedPagedCollectionResponse_CanBeCreated()
    {
        // Arrange
        var item = new ListPublishedStories.ListPublishedStoriesItem(
            Ulid.NewUlid(), "Story 1", "Description 1", DateTime.UtcNow, DateTime.UtcNow,
            true, false, false, false, 0, Ulid.NewUlid(), "Author 1");

        var selfLink = new LinkItem("https://example.com/stories/1", "self", "GET");
        var linkedItem = new Linked<ListPublishedStories.ListPublishedStoriesItem>(item, [selfLink]);

        var collectionLinks = new List<LinkItem>
        {
            new("https://example.com/stories?page=2", "self", "GET"),
            new("https://example.com/stories?page=3", "next-page", "GET"),
            new("https://example.com/stories?page=1", "previous-page", "GET")
        };

        // Act
        var response = new LinkedPagedCollection<ListPublishedStories.ListPublishedStoriesItem>(
            new[] { linkedItem }.AsQueryable(),
            25,
            2,
            10,
            collectionLinks);

        // Assert
        Assert.Single(response.Data);
        Assert.Equal(25, response.TotalCount);
        Assert.Equal(2, response.CurrentPage);
        Assert.Equal(10, response.PageSize);
        Assert.Equal(3, response.TotalPages);
        Assert.Equal(3, response.Links.Count());
    }

    [Fact]
    public void LinkedPagedCollectionResponse_Items_HaveLinks()
    {
        // Arrange
        var item = new ListPublishedStories.ListPublishedStoriesItem(
            Ulid.NewUlid(), "Story 1", "Description 1", DateTime.UtcNow, DateTime.UtcNow,
            true, false, false, false, 0, Ulid.NewUlid(), "Author 1");

        var selfLink = new LinkItem("https://example.com/stories/1", "self", "GET");
        var linkedItem = new Linked<ListPublishedStories.ListPublishedStoriesItem>(item, [selfLink]);

        // Act / Assert
        Assert.Single(linkedItem.Links);
        Assert.Equal("self", linkedItem.Links.First().Rel);
        Assert.Equal("GET", linkedItem.Links.First().Method);
        Assert.Equal("https://example.com/stories/1", linkedItem.Links.First().Href);
    }

    [Fact]
    public void LinkedPagedCollectionResponse_CollectionLinks_ContainSelfLink()
    {
        // Arrange
        var collectionLinks = new List<LinkItem>
        {
            new("https://example.com/stories?page=2", "self", "GET"),
            new("https://example.com/stories?page=3", "next-page", "GET"),
            new("https://example.com/stories?page=1", "previous-page", "GET")
        };

        var response = new LinkedPagedCollection<ListPublishedStories.ListPublishedStoriesItem>(
            Array.Empty<Linked<ListPublishedStories.ListPublishedStoriesItem>>().AsQueryable(),
            25, 2, 10, collectionLinks);

        // Act / Assert
        var selfLinks = response.Links.Where(l => l.Rel == "self").ToList();
        Assert.Single(selfLinks);
        Assert.Equal("GET", selfLinks[0].Method);
    }

    [Fact]
    public void LinkedPagedCollectionResponse_MultiPage_CollectionLinks_IncludeNextPageLink()
    {
        // Arrange — page 1 of 3 → next-page link should be present
        var collectionLinks = new List<LinkItem>
        {
            new("https://example.com/stories?page=1", "self", "GET"),
            new("https://example.com/stories?page=2", "next-page", "GET")
        };

        var response = new LinkedPagedCollection<ListPublishedStories.ListPublishedStoriesItem>(
            Array.Empty<Linked<ListPublishedStories.ListPublishedStoriesItem>>().AsQueryable(),
            25, 1, 10, collectionLinks);

        // Act / Assert
        Assert.True(response.CurrentPage < response.TotalPages);
        Assert.Contains(response.Links, l => l.Rel == "next-page");
        Assert.DoesNotContain(response.Links, l => l.Rel == "previous-page");
    }

    [Fact]
    public void LinkedPagedCollectionResponse_NonFirstPage_CollectionLinks_IncludePreviousPageLink()
    {
        // Arrange — last page of 3 → previous-page link should be present
        var collectionLinks = new List<LinkItem>
        {
            new("https://example.com/stories?page=3", "self", "GET"),
            new("https://example.com/stories?page=2", "previous-page", "GET")
        };

        var response = new LinkedPagedCollection<ListPublishedStories.ListPublishedStoriesItem>(
            Array.Empty<Linked<ListPublishedStories.ListPublishedStoriesItem>>().AsQueryable(),
            25, 3, 10, collectionLinks);

        // Act / Assert
        Assert.True(response.CurrentPage > 1);
        Assert.Contains(response.Links, l => l.Rel == "previous-page");
        Assert.DoesNotContain(response.Links, l => l.Rel == "next-page");
    }

    [Fact]
    public void LinkedPagedCollectionResponse_SinglePage_HasOnlySelfLink()
    {
        // Arrange — all results fit on one page → only self link
        var collectionLinks = new List<LinkItem>
        {
            new("https://example.com/stories?page=1", "self", "GET")
        };

        var response = new LinkedPagedCollection<ListPublishedStories.ListPublishedStoriesItem>(
            Array.Empty<Linked<ListPublishedStories.ListPublishedStoriesItem>>().AsQueryable(),
            5, 1, 10, collectionLinks);

        // Act / Assert
        Assert.Equal(1, response.TotalPages);
        Assert.Single(response.Links);
        Assert.Equal("self", response.Links.Single().Rel);
    }

    [Fact]
    public void WithLinks_FirstPage_AddsSelfAndNextOnly()
    {
        // Arrange
        var stories = new[]
        {
            new ListPublishedStories.ListPublishedStoriesItem(
                Ulid.NewUlid(), "Story 1", "Description 1", DateTime.UtcNow, DateTime.UtcNow,
                true, false, false, false, 0, Ulid.NewUlid(), "Author 1")
        };

        var paged = new PagedCollection<ListPublishedStories.ListPublishedStoriesItem>(
            stories.AsQueryable(),
            25,
            1,
            10);

        var linker = CreateLinkService();
        var query = new ListPublishedStories.ListPublishedStoriesQuery(Page: 1, PageSize: 10, Search: "fantasy", Sort: "publishedAt");

        // Act
        var linked = paged.WithLinks(
            linker,
            ListPublishedStories.EndpointName,
            item => new(item, Enumerable.Empty<LinkItem>()),
            query);

        // Assert
        Assert.Contains(linked.Links, l => l.Rel == "self");
        Assert.Contains(linked.Links, l => l.Rel == "next-page");
        Assert.DoesNotContain(linked.Links, l => l.Rel == "previous-page");
    }

    [Fact]
    public void WithLinks_MiddlePage_AddsSelfNextAndPrevious()
    {
        // Arrange
        var stories = new[]
        {
            new ListPublishedStories.ListPublishedStoriesItem(
                Ulid.NewUlid(), "Story 1", "Description 1", DateTime.UtcNow, DateTime.UtcNow,
                true, false, false, false, 0, Ulid.NewUlid(), "Author 1")
        };

        var paged = new PagedCollection<ListPublishedStories.ListPublishedStoriesItem>(
            stories.AsQueryable(),
            25,
            2,
            10);

        var linker = CreateLinkService();
        var query = new ListPublishedStories.ListPublishedStoriesQuery(Page: 2, PageSize: 10, Search: "fantasy", Sort: "publishedAt");

        // Act
        var linked = paged.WithLinks(
            linker,
            ListPublishedStories.EndpointName,
            item => new(item, Enumerable.Empty<LinkItem>()),
            query);

        // Assert
        Assert.Contains(linked.Links, l => l.Rel == "self");
        Assert.Contains(linked.Links, l => l.Rel == "next-page");
        Assert.Contains(linked.Links, l => l.Rel == "previous-page");
    }

    [Fact]
    public void WithLinks_LastPage_AddsSelfAndPreviousOnly()
    {
        // Arrange
        var stories = new[]
        {
            new ListPublishedStories.ListPublishedStoriesItem(
                Ulid.NewUlid(), "Story 1", "Description 1", DateTime.UtcNow, DateTime.UtcNow,
                true, false, false, false, 0, Ulid.NewUlid(), "Author 1")
        };

        var paged = new PagedCollection<ListPublishedStories.ListPublishedStoriesItem>(
            stories.AsQueryable(),
            25,
            3,
            10);

        var linker = CreateLinkService();
        var query = new ListPublishedStories.ListPublishedStoriesQuery(Page: 3, PageSize: 10, Search: "fantasy", Sort: "publishedAt");

        // Act
        var linked = paged.WithLinks(
            linker,
            ListPublishedStories.EndpointName,
            item => new(item, Enumerable.Empty<LinkItem>()),
            query);

        // Assert
        Assert.Contains(linked.Links, l => l.Rel == "self");
        Assert.DoesNotContain(linked.Links, l => l.Rel == "next-page");
        Assert.Contains(linked.Links, l => l.Rel == "previous-page");
    }

    private static LinkService CreateLinkService()
    {
        var linkGenerator = new FakeLinkGenerator();
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

        return new LinkService(linkGenerator, httpContextAccessor);
    }

    private sealed class FakeLinkGenerator : LinkGenerator
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
        {
            var page = values.TryGetValue(nameof(IPaginationSupport.Page), out var pageValue)
                ? pageValue?.ToString()
                : "";

            var pageSize = values.TryGetValue(nameof(IPaginationSupport.PageSize), out var pageSizeValue)
                ? pageSizeValue?.ToString()
                : "";

            return $"/stories/published?page={page}&pageSize={pageSize}";
        }

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
            var page = values.TryGetValue(nameof(IPaginationSupport.Page), out var pageValue)
                ? pageValue?.ToString()
                : "";

            var pageSize = values.TryGetValue(nameof(IPaginationSupport.PageSize), out var pageSizeValue)
                ? pageSizeValue?.ToString()
                : "";

            return $"https://example.test/stories/published?page={page}&pageSize={pageSize}";
        }
    }
}
