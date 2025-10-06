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
    public void ChapterItem_CanBeCreated()
    {
        // Arrange
        var chapterId = Ulid.NewUlid();
        var publishedAt = DateTime.UtcNow;
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var updatedAt = DateTime.UtcNow;

        // Act
        var item = new ListStoryChapters.ListStoryChaptersItem(
            chapterId,
            "Chapter 1: The Beginning",
            0,    // Order
            publishedAt,
            createdAt,
            updatedAt,
            true,  // HasContent
            1500); // ContentLength

        // Assert
        Assert.Equal(chapterId, item.ChapterId);
        Assert.Equal("Chapter 1: The Beginning", item.Title);
        Assert.Equal(publishedAt, item.PublishedAt);
        Assert.Equal(createdAt, item.CreatedAt);
        Assert.Equal(updatedAt, item.UpdatedAt);
        Assert.True(item.HasContent);
        Assert.Equal(1500, item.ContentLength);
    }

    [Fact]
    public void ChapterItem_WithNullPublishedAt_CanBeCreated()
    {
        // Arrange
        var chapterId = Ulid.NewUlid();
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var updatedAt = DateTime.UtcNow;

        // Act
        var item = new ListStoryChapters.ListStoryChaptersItem(
            chapterId,
            "Draft Chapter",
            0,    // Order
            null, // Not published
            createdAt,
            updatedAt,
            false, // No content yet
            0);    // No content length

        // Assert
        Assert.Equal(chapterId, item.ChapterId);
        Assert.Equal("Draft Chapter", item.Title);
        Assert.Null(item.PublishedAt);
        Assert.Equal(createdAt, item.CreatedAt);
        Assert.Equal(updatedAt, item.UpdatedAt);
        Assert.False(item.HasContent);
        Assert.Equal(0, item.ContentLength);
    }

    [Fact]
    public void CollectionResponse_WithMultipleChapters_CanBeCreated()
    {
        // Arrange
        var chapters = new List<ListStoryChapters.ListStoryChaptersItem>
        {
            new(Ulid.NewUlid(), "Chapter 1", 0, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, true, 1000),
            new(Ulid.NewUlid(), "Chapter 2", 1, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, false, 0) // Published chapters only
        };

        // Act
        var response = new CollectionResponse<ListStoryChapters.ListStoryChaptersItem>(chapters.AsQueryable());

        // Assert
        Assert.Equal(2, response.Data.Count());
        Assert.Equal("Chapter 1", response.Data.First().Title);
        Assert.Equal("Chapter 2", response.Data.Last().Title);
    }

    [Fact]
    public void CollectionResponse_WithSingleChapter_CanBeCreated()
    {
        // Arrange
        var chapters = new List<ListStoryChapters.ListStoryChaptersItem>
        {
            new(Ulid.NewUlid(), "Chapter 1", 0, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, true, 1000)
        };

        // Act
        var response = new CollectionResponse<ListStoryChapters.ListStoryChaptersItem>(chapters.AsQueryable());

        // Assert
        Assert.Single(response.Data);
        Assert.Equal("Chapter 1", response.Data.First().Title);
    }


}
