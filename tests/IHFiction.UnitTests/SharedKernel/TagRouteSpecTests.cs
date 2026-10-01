using IHFiction.SharedKernel.Searching;

namespace IHFiction.UnitTests.SharedKernel;

public sealed class TagRouteSpecTests
{
    [Theory]
    [InlineData("genre:science-fiction", 1)]
    [InlineData("genre:science-fiction,character:main:harry-potter", 2)]
    [InlineData("genre:science-fiction+character:main:harry-potter", 2)]
    [InlineData("harry_potter", 1)]
    public void TryParse_ValidSpecifications_ReturnsTokens(string input, int count)
    {
        Assert.True(TagRouteSpec.TryParse(input, out var tokens, out var error));
        Assert.Equal(TagRouteSpecError.None, error);
        Assert.Equal(count, tokens.Count);
    }

    [Theory]
    [InlineData("genre:science-fiction,theme:found-family,tone:hopeful,rating:teen", TagRouteSpecError.TooManyTags)]
    [InlineData("genre:", TagRouteSpecError.InvalidFormat)]
    [InlineData(":fantasy", TagRouteSpecError.InvalidFormat)]
    [InlineData("genre::fantasy", TagRouteSpecError.InvalidFormat)]
    [InlineData("genre:sub:value:extra", TagRouteSpecError.InvalidFormat)]
    [InlineData("genre:fantasy,", TagRouteSpecError.InvalidFormat)]
    public void TryParse_InvalidSpecifications_ReturnsActionableError(string input, TagRouteSpecError expected)
    {
        Assert.False(TagRouteSpec.TryParse(input, out var tokens, out var error));
        Assert.Empty(tokens);
        Assert.Equal(expected, error);
    }

    [Fact]
    public void TryParse_OverLengthSpecification_IsRejected()
    {
        Assert.False(TagRouteSpec.TryParse(new string('a', TagRouteSpec.MaxLength + 1), out _, out var error));
        Assert.Equal(TagRouteSpecError.TooLong, error);
    }

    [Theory]
    [InlineData("Harry Potter", "harry-potter")]
    [InlineData("harry_potter", "harry-potter")]
    [InlineData("  Science  Fiction  ", "science-fiction")]
    [InlineData("C++", "c")]
    public void SlugifyAscii_UsesReadableHyphenatedWords(string input, string expected)
    {
        Assert.Equal(expected, TagRouteSpec.SlugifyAscii(input));
    }

    [Fact]
    public void CreateRouteToken_UsesDisplayValuesForAsciiTag()
    {
        var routeToken = TagRouteSpec.CreateRouteToken(
            Ulid.Parse("01M3QDT1KS6Z4DWG0ATBD7Q4NF"),
            "Character",
            "Main",
            "Harry Potter");

        Assert.Equal("character:main:harry-potter", routeToken);
    }

    [Fact]
    public void CreateRouteToken_UsesStableIdForNonAsciiTag()
    {
        var id = Ulid.Parse("01M3QDT1KS6Z4DWG0ATBD7Q4NF");

        var routeToken = TagRouteSpec.CreateRouteToken(id, "fandom", null, "Pokémon");

        Assert.Equal($"id:{id.ToString().ToLowerInvariant()}", routeToken);
    }

    [Fact]
    public void BuildCanonicalSpec_SortsAndDeduplicatesUnorderedTags()
    {
        var canonical = TagRouteSpec.BuildCanonicalSpec([
            "universe:kantai-collection",
            "genre:science-fiction",
            "universe:kantai-collection"
        ]);

        Assert.Equal("genre:science-fiction,universe:kantai-collection", canonical);
        Assert.Equal("/tags/genre:science-fiction,universe:kantai-collection", TagRouteSpec.BuildPath(canonical));
    }
}
