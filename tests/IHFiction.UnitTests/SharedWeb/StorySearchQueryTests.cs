using FluentAssertions;

using IHFiction.SharedWeb.Components.Stories;

using Microsoft.AspNetCore.WebUtilities;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class StorySearchQueryTests
{
    [Theory]
    [InlineData("ghost", "ghost")]
    [InlineData("  ghost story  ", "ghost story")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_TrimsAndNormalizesQuery(string? input, string expected)
    {
        StorySearchQuery.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddCanonical_OmitsEmptySearch(string? search)
    {
        var query = new Dictionary<string, string?>();

        StorySearchQuery.AddCanonical(query, search);

        query.Should().BeEmpty();
    }

    [Fact]
    public void AddCanonical_ProducesEncodedQParameter()
    {
        var query = new Dictionary<string, string?>();
        StorySearchQuery.AddCanonical(query, "ghost story & sequel");

        var url = QueryHelpers.AddQueryString("/stories", query);

        url.Should().Be("/stories?q=ghost%20story%20%26%20sequel");
    }
}

