using IHFiction.SharedWeb.Components.Tags;

namespace IHFiction.UnitTests.SharedWeb;

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
}
