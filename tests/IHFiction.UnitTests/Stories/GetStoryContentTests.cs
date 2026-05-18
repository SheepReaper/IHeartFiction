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

}
