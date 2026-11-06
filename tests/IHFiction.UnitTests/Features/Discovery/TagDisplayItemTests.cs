using IHFiction.SharedWeb.Features.Discovery;
namespace IHFiction.UnitTests.Features.Discovery;

public sealed class TagDisplayItemTests
{
    [Fact]
    public void Group_SortsCategoriesSubcategoriesAndValuesCaseInsensitively()
    {
        TagDisplayItem[] tags =
        [
            new("theme", "Relationship", "Rivals", "theme:Relationship:Rivals"),
            new("genre", null, "Fantasy", "genre:Fantasy"),
            new("theme", null, "Adventure", "theme:Adventure"),
            new("theme", "Character", "Hero", "theme:Character:Hero"),
            new("genre", null, "Action", "genre:Action")
        ];

        var groups = TagDisplayItem.Group(tags);

        Assert.Equal(["genre", "theme"], groups.Select(group => group.Category));
        Assert.Equal(["Action", "Fantasy"], groups[0].Sections[0].Tags.Select(tag => tag.Value));
        Assert.Equal([null, "Character", "Relationship"], groups[1].Sections.Select(section => section.Subcategory));
    }

    [Fact]
    public void Group_UnifiesUnderscoreAndHyphenCategoriesAndNormalizesDisplayValues()
    {
        TagDisplayItem[] tags =
        [
            new("content_warning", "violence", "mild_violence", "content-warning:violence:mild-violence"),
            new("content-warning", "violence", "graphic violence", "content-warning:violence:graphic-violence"),
            new("character version", "harry_potter", "adult_harry_potter", "character-version:harry-potter:adult-harry-potter")
        ];

        var groups = TagDisplayItem.Group(tags);

        Assert.Equal(["character-version", "content-warning"], groups.Select(group => group.Category));
        var warningGroup = Assert.Single(groups, group => group.Category == "content-warning");
        var violenceSection = Assert.Single(warningGroup.Sections);
        Assert.Equal("violence", violenceSection.Subcategory);
        Assert.Equal(["graphic violence", "mild violence"], violenceSection.Tags.Select(tag => tag.Value));
        Assert.True(TagDisplayItem.IsContentWarningCategory("content_warning"));
        Assert.True(TagDisplayItem.IsContentWarningCategory("content-warning"));
    }
}
