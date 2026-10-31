using IHFiction.FictionApi.Features.Authoring;
using IHFiction.FictionApi.Features.Following;
using IHFiction.FictionApi.Features.Reading;

using Wolverine;

namespace IHFiction.UnitTests.FictionApi;

public sealed class DurableMessageCompatibilityTests
{
    public static TheoryData<object, string> Messages => new()
    {
        { new TagCreatedRequested(Ulid.NewUlid()), "IHFiction.FictionApi.Stories.TagCreatedRequested" },
        { new RecordWorkReadRequested(Ulid.NewUlid(), null, null, DateTime.UtcNow), "IHFiction.FictionApi.Stories.RecordWorkReadRequested" },
        { new StoryPublishedNotificationRequested(Ulid.NewUlid()), "IHFiction.FictionApi.Notifications.StoryPublishedNotificationRequested" },
        { new StoryCompletedNotificationRequested(Ulid.NewUlid()), "IHFiction.FictionApi.Notifications.StoryCompletedNotificationRequested" },
        { new ChapterPublishedNotificationRequested(Ulid.NewUlid()), "IHFiction.FictionApi.Notifications.ChapterPublishedNotificationRequested" }
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void Envelope_PreservesIdentityUsedByPersistedMessages(object message, string expectedIdentity)
    {
        Assert.Equal(expectedIdentity, new Envelope(message).MessageType);
    }
}
