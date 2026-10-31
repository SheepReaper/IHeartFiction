using IHFiction.FictionApi.Features.Discovery;

namespace IHFiction.UnitTests.Features.Discovery;

public sealed class TagCanonicalizationServiceTests
{
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
}
