using IHFiction.SharedWeb.Components.Tags;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class TagInputParserTests
{
    [Fact]
    public void Parse_SeparatesNewlinesCommasAndSemicolonsButPreservesSpaces()
    {
        const string input = "genre:Science Fiction, theme:Found Family;warning:Graphic Violence\r\nuniverse:Alternate Earth";

        var tags = TagInputParser.Parse(input);

        Assert.Equal(
            ["genre:Science Fiction", "theme:Found Family", "warning:Graphic Violence", "universe:Alternate Earth"],
            tags);
    }

    [Fact]
    public void Parse_RemovesEmptyAndEquivalentDuplicateValues()
    {
        const string input = "genre:Science Fiction,, Genre:science-fiction ;\n";

        var tags = TagInputParser.Parse(input);

        Assert.Equal(["genre:Science Fiction"], tags);
    }
}
