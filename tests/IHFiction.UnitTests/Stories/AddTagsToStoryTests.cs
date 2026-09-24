using IHFiction.FictionApi.Stories;
using IHFiction.FictionApi.Tags;

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
        // Act
        var response = new AddTagsToStory.AddTagsToStoryResponse(
            storyId,
            "My Story",
            addedTags,
            5);

        // Assert
        Assert.Equal(storyId, response.StoryId);
        Assert.Equal("My Story", response.StoryTitle);
        Assert.Equal(2, response.Tags.Count);
        Assert.Equal(5, response.TotalTags);
        Assert.Equal("genre", response.Tags[0].Category);
        Assert.Equal("fantasy", response.Tags[0].Value);
        Assert.True(response.Tags[0].IsNew);
        Assert.False(response.Tags[1].IsNew);
    }

    [Fact]
    public void TagCanonicalizationService_NormalizesEquivalentTagVariants()
    {
        var canonicalKeyA = TagCanonicalizationService.BuildKey("universe", null, "HarryPotter");
        var canonicalKeyB = TagCanonicalizationService.BuildKey("universe", null, "harry_potter");
        var canonicalKeyC = TagCanonicalizationService.BuildKey("universe", null, "harry-potter");

        Assert.Equal("universe:harrypotter", canonicalKeyA);
        Assert.Equal(canonicalKeyA, canonicalKeyB);
        Assert.Equal(canonicalKeyA, canonicalKeyC);
        Assert.True(TagCanonicalizationService.Matches("universe", null, "HarryPotter", "universe", null, "harry_potter"));
    }

    [Theory]
    [InlineData("Universe", null, "Harry Potter", "universe:harrypotter")]
    [InlineData("genre", "Urban Fantasy", "Magic-Punk", "genre:urbanfantasy:magicpunk")]
    public void Tag_NormalizedKey_IsStableAcrossDisplayFormatting(
        string category,
        string? subcategory,
        string value,
        string expected)
    {
        Assert.Equal(expected, Data.Searching.Domain.Tag.BuildNormalizedKey(category, subcategory, value));
    }

    [Fact]
    public void CanonicalTag_Rename_UpdatesDisplayValuesAndNormalizedKey()
    {
        var tag = Data.Searching.Domain.Tag.CreateCanonical("genre", null, "Sci Fi");

        tag.Rename("Genre", "Speculative", "Science-Fiction");

        Assert.Equal("Genre", tag.Category);
        Assert.Equal("Speculative", tag.Subcategory);
        Assert.Equal("Science-Fiction", tag.Value);
        Assert.Equal("genre:speculative:sciencefiction", tag.NormalizedKey);
    }
}
