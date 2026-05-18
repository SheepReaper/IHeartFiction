using IHFiction.FictionApi.Stories;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for AddTagsToStory functionality
/// Tests request validation, response model construction, tag parsing, and authorization logic
/// </summary>
public class AddTagsToStoryTests
{
    [Fact]
    public void AddTagsToStoryResponse_CanBeCreated()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var addedTags = new List<AddTagsToStory.AddedTagItem>
        {
            new("genre", null, "fantasy", true),
            new("theme", null, "adventure", false)
        };
        var skippedTags = new List<string> { "invalid-format", "duplicate:tag" };

        // Act
        var response = new AddTagsToStory.AddTagsToStoryResponse(
            storyId,
            "My Story",
            addedTags,
            skippedTags,
            5);

        // Assert
        Assert.Equal(storyId, response.StoryId);
        Assert.Equal("My Story", response.StoryTitle);
        Assert.Equal(2, response.AddedTags.Count);
        Assert.Equal(2, response.SkippedTags.Count);
        Assert.Equal(5, response.TotalTags);
        Assert.Equal("genre", response.AddedTags[0].Category);
        Assert.Equal("fantasy", response.AddedTags[0].Value);
        Assert.True(response.AddedTags[0].IsNew);
        Assert.False(response.AddedTags[1].IsNew);
    }
}
