using IHFiction.FictionApi.Stories;

using MongoDB.Bson;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for GetStoryContent functionality
/// Tests response model construction and error codes
/// </summary>
public class GetStoryContentTests
{
    [Fact]
    public void GetStoryContentResponse_CanBeCreated()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var contentId = ObjectId.GenerateNewId();
        var contentUpdatedAt = DateTime.UtcNow;
        var storyUpdatedAt = DateTime.UtcNow.AddMinutes(1);

        // Act
        var response = new GetPublishedStoryContent.GetPublishedStoryContentResponse(
            storyId,
            "Test Story",
            "A test story description",
            contentId,
            "This is the story content with **markdown** formatting.",
            "Author's note",
            "Additional note",
            contentUpdatedAt,
            storyUpdatedAt);

        // Assert
        Assert.Equal(storyId, response.StoryId);
        Assert.Equal("Test Story", response.StoryTitle);
        Assert.Equal(contentId, response.ContentId);
        Assert.Equal("This is the story content with **markdown** formatting.", response.Content);
        Assert.Equal("Author's note", response.Note1);
        Assert.Equal("Additional note", response.Note2);
        Assert.Equal(contentUpdatedAt, response.ContentUpdatedAt);
        Assert.Equal(storyUpdatedAt, response.StoryUpdatedAt);
    }

    [Fact]
    public void GetStoryContentResponse_WithPublishedContent_CanBeCreated()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var contentId = ObjectId.GenerateNewId();
        var now = DateTime.UtcNow;

        // Act
        var response = new GetPublishedStoryContent.GetPublishedStoryContentResponse(
            storyId,
            "Published Story",
            "A published story with content.",
            contentId,
            "Content from a published story.",
            null,
            null,
            now,
            now);

        // Assert
        Assert.Equal(storyId, response.StoryId);
        Assert.Equal("Published Story", response.StoryTitle);
        Assert.Equal(contentId, response.ContentId);
        Assert.Equal("Content from a published story.", response.Content);
        Assert.Null(response.Note1);
        Assert.Null(response.Note2);
        Assert.Equal(now, response.ContentUpdatedAt);
        Assert.Equal(now, response.StoryUpdatedAt);
    }
}
