using IHFiction.FictionApi.Features.Reading;
namespace IHFiction.UnitTests.Features.Reading;

public sealed class ReadIdentityTests
{
    [Fact]
    public void ForDevice_IsStableAndDoesNotRetainRawIdentifier()
    {
        const string deviceId = "browser-device-123";

        var first = ReadIdentity.ForDevice(deviceId);
        var second = ReadIdentity.ForDevice(deviceId);

        Assert.Equal(first, second);
        Assert.StartsWith("d:", first, StringComparison.Ordinal);
        Assert.DoesNotContain(deviceId, first, StringComparison.Ordinal);
    }

    [Fact]
    public void ForUser_UsesCanonicalLowercaseGuid()
    {
        var userId = Guid.Parse("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE");

        Assert.Equal("u:AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE", ReadIdentity.ForUser(userId));
    }
}
