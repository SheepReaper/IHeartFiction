using IHFiction.FictionApi.Stories;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for PublishStory functionality
/// Tests response model construction and error codes
/// </summary>
public class PublishStoryTests
{
    [Fact]
    public void PublishStoryResponse_CanBeCreated()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var publishedAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow.AddMinutes(1);

        // Act
        var response = new PublishStory.PublishStoryResponse(
            storyId,
            "Test Story",
            "A test story description",
            publishedAt,
            updatedAt,
            true,  // HasContent
            false, // HasChapters
            false, // HasBooks
            0);    // ChapterCount

        // Assert
        Assert.Equal(storyId, response.StoryId);
        Assert.Equal("Test Story", response.Title);
        Assert.Equal("A test story description", response.Description);
        Assert.Equal(publishedAt, response.PublishedAt);
        Assert.Equal(updatedAt, response.UpdatedAt);
        Assert.True(response.HasContent);
        Assert.False(response.HasChapters);
        Assert.False(response.HasBooks);
        Assert.Equal(0, response.ChapterCount);
    }

    [Fact]
    public void PublishStoryResponse_WithChapters_ShowsCorrectStructure()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var publishedAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow.AddMinutes(1);

        // Act
        var response = new PublishStory.PublishStoryResponse(
            storyId,
            "Chapter-based Story",
            "A story with chapters",
            publishedAt,
            updatedAt,
            false, // HasContent
            true,  // HasChapters
            false, // HasBooks
            5);    // ChapterCount

        // Assert
        Assert.False(response.HasContent);
        Assert.True(response.HasChapters);
        Assert.False(response.HasBooks);
        Assert.Equal(5, response.ChapterCount);
    }

    [Fact]
    public void PublishStoryResponse_WithBooks_ShowsCorrectStructure()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var publishedAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow.AddMinutes(1);

        // Act
        var response = new PublishStory.PublishStoryResponse(
            storyId,
            "Series Story",
            "A story series with books",
            publishedAt,
            updatedAt,
            false, // HasContent
            false, // HasChapters
            true,  // HasBooks
            0);    // ChapterCount (books have their own chapters)

        // Assert
        Assert.False(response.HasContent);
        Assert.False(response.HasChapters);
        Assert.True(response.HasBooks);
        Assert.Equal(0, response.ChapterCount);
    }


}
