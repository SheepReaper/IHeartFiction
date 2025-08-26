using IHFiction.FictionApi.Stories;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for UnpublishStory functionality
/// Tests response model construction and error codes
/// </summary>
public class UnpublishStoryTests
{
    [Fact]
    public void UnpublishStoryResponse_CanBeCreated()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var updatedAt = DateTime.UtcNow;

        // Act
        var response = new UnpublishStory.UnpublishStoryResponse(
            storyId,
            "Test Story",
            "A test story description",
            updatedAt,
            true,  // HasContent
            false, // HasChapters
            false, // HasBooks
            0);    // ChapterCount

        // Assert
        Assert.Equal(storyId, response.StoryId);
        Assert.Equal("Test Story", response.Title);
        Assert.Equal("A test story description", response.Description);
        Assert.Equal(updatedAt, response.UpdatedAt);
        Assert.True(response.HasContent);
        Assert.False(response.HasChapters);
        Assert.False(response.HasBooks);
        Assert.Equal(0, response.ChapterCount);
    }

    [Fact]
    public void UnpublishStoryResponse_WithChapters_ShowsCorrectStructure()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var updatedAt = DateTime.UtcNow;

        // Act
        var response = new UnpublishStory.UnpublishStoryResponse(
            storyId,
            "Chapter-based Story",
            "A story with chapters",
            updatedAt,
            false, // HasContent
            true,  // HasChapters
            false, // HasBooks
            3);    // ChapterCount

        // Assert
        Assert.False(response.HasContent);
        Assert.True(response.HasChapters);
        Assert.False(response.HasBooks);
        Assert.Equal(3, response.ChapterCount);
    }

    [Fact]
    public void UnpublishStoryResponse_WithBooks_ShowsCorrectStructure()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var updatedAt = DateTime.UtcNow;

        // Act
        var response = new UnpublishStory.UnpublishStoryResponse(
            storyId,
            "Series Story",
            "A story series with books",
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
