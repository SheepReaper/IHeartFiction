using IHFiction.SharedWeb.Features.Reading;
namespace IHFiction.UnitTests.Features.Reading;

public sealed class ReadCountFormatterTests
{
    [Theory]
    [InlineData(999, "999")]
    [InlineData(1_000, "1K")]
    [InlineData(1_250, "1.3K")]
    [InlineData(1_200_000, "1.2M")]
    public void Format_UsesCompactDisplayAtOneThousand(int count, string expected) =>
        Assert.Equal(expected, ReadCountFormatter.Format(count));
}
