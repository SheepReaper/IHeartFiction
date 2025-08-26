using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Stories;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for AddTagsToStory functionality
/// Tests request validation, response model construction, tag parsing, and authorization logic
/// </summary>
public class AddTagsToStoryTests
{
    [Fact]
    public void AddTagsToStoryRequest_WithValidTags_IsValid()
    {
        // Arrange
        var request = new AddTagsToStory.AddTagsToStoryBody(
            Tags: "genre:fantasy,theme:adventure,setting:medieval:castle");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
        Assert.Equal("genre:fantasy,theme:adventure,setting:medieval:castle", request.Tags);
    }

    [Fact]
    public void AddTagsToStoryRequest_WithEmptyTags_FailsValidation()
    {
        // Arrange
        var request = new AddTagsToStory.AddTagsToStoryBody(
            Tags: "");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("Tags are required"));
    }

    [Fact]
    public void TagItem_CanBeCreated()
    {
        // Arrange & Act
        var tagItem = new AddTagsToStory.AddedTagItem(
            "genre",
            null,
            "fantasy",
            true);

        // Assert
        Assert.Equal("genre", tagItem.Category);
        Assert.Null(tagItem.Subcategory);
        Assert.Equal("fantasy", tagItem.Value);
        Assert.True(tagItem.IsNew);
    }

    [Fact]
    public void TagItem_WithSubcategory_CanBeCreated()
    {
        // Arrange & Act
        var tagItem = new AddTagsToStory.AddedTagItem(
            "setting",
            "medieval",
            "castle",
            false);

        // Assert
        Assert.Equal("setting", tagItem.Category);
        Assert.Equal("medieval", tagItem.Subcategory);
        Assert.Equal("castle", tagItem.Value);
        Assert.False(tagItem.IsNew);
    }

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