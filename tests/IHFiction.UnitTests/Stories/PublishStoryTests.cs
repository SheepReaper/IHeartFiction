using IHFiction.FictionApi.Stories;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for PublishStory functionality
/// Tests response model construction and error codes
/// </summary>
public class PublishWorkTests
{
    [Fact]
    public void PublishWorkResponse_CanBeCreated()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var publishedAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow.AddMinutes(1);

        // Act
        var response = new PublishWork.PublishWorkResponse(
            storyId,
            "Test Story",
            "A test story description",
            publishedAt,
            updatedAt,
            true,  // HasContent
            false, // HasChapters
            0);    // ChapterCount

        // Assert
        Assert.Equal(storyId, response.WorkId);
        Assert.Equal("Test Story", response.Title);
        Assert.Equal("A test story description", response.Type);
        Assert.Equal(publishedAt, response.PublishedAt);
        Assert.Equal(updatedAt, response.UpdatedAt);
        Assert.True(response.HasContent);
        Assert.False(response.HasChildren);
        Assert.Equal(0, response.ChildCount);
    }

    [Fact]
    public void PublishStoryResponse_WithChapters_ShowsCorrectStructure()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var publishedAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow.AddMinutes(1);

        // Act
        var response = new PublishWork.PublishWorkResponse(
            storyId,
            "Chapter-based Story",
            "A story with chapters",
            publishedAt,
            updatedAt,
            false, // HasContent
            true,  // HasChapters
            5);    // ChapterCount

        // Assert
        Assert.False(response.HasContent);
        Assert.True(response.HasChildren);
        Assert.Equal(5, response.ChildCount);
    }

    [Fact]
    public void PublishStoryResponse_WithBooks_ShowsCorrectStructure()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var publishedAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow.AddMinutes(1);

        // Act
        var response = new PublishWork.PublishWorkResponse(
            storyId,
            "Series Story",
            "A story series with books",
            publishedAt,
            updatedAt,
            false, // HasContent
            false, // HasChapters
            0);    // ChapterCount (books have their own chapters)

        // Assert
        Assert.False(response.HasContent);
        Assert.False(response.HasChildren);
        Assert.Equal(0, response.ChildCount);
    }


}
