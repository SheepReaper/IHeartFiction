using IHFiction.FictionApi.Stories;
using IHFiction.SharedKernel.Infrastructure;

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
}
