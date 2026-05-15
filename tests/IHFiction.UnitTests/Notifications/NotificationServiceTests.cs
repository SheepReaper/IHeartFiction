using FluentAssertions;
using NSubstitute;

using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Services;

namespace IHFiction.UnitTests.Notifications;

public class NotificationServiceTests
{
    private const string DeviceId = "device-123";

    [Fact]
    public async Task GetNotificationsAsync_WhenAuthenticated_MergesOwnAndDeviceItems()
    {
        var sharedNotificationId = Ulid.NewUlid();
        var ownOnlyNotificationId = Ulid.NewUlid();
        var deviceOnlyNotificationId = Ulid.NewUlid();
        var now = DateTimeOffset.UtcNow;

        var client = Substitute.For<IFictionApiClient>();
        client.GetOwnNotificationsAsync(Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetOwnNotificationsResponse
            {
                UnreadCount = 1,
                Items =
                {
                    CreateOwnNotification(sharedNotificationId, now.AddMinutes(-10), now.AddMinutes(-5)),
                    CreateOwnNotification(ownOnlyNotificationId, now.AddMinutes(-30), null)
                }
            });

        client.GetDeviceNotificationsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetDeviceNotificationsResponse
            {
                UnreadCount = 2,
                Items =
                {
                    CreateDeviceNotification(sharedNotificationId, now.AddMinutes(-2), null),
                    CreateDeviceNotification(deviceOnlyNotificationId, now.AddMinutes(-1), null)
                }
            });

        var service = CreateService(client);

        var result = await service.GetNotificationsAsync(true, limit: 10, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var inbox = result.Value!;
        inbox.Items.Should().HaveCount(3);
        inbox.Items.Select(item => item.NotificationId).Should().Contain([sharedNotificationId, ownOnlyNotificationId, deviceOnlyNotificationId]);
        inbox.Items.Should().BeInDescendingOrder(item => item.DeliveredAt);
        inbox.Items.Single(item => item.NotificationId == sharedNotificationId).ReadAt.Should().BeNull();
        inbox.UnreadCount.Should().Be(3);
        await client.Received(1).GetDeviceNotificationsAsync(DeviceId, 10, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFollowSnapshotAsync_WhenAuthenticated_MergesOwnAndDeviceFollows()
    {
        var sharedAuthorId = Ulid.NewUlid();
        var ownStoryId = Ulid.NewUlid();
        var deviceStoryId = Ulid.NewUlid();

        var client = Substitute.For<IFictionApiClient>();
        client.GetOwnFollowsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetOwnFollowsResponse
            {
                Authors =
                {
                    new OwnFollowedAuthorItem { AuthorId = sharedAuthorId, Name = "Own Author" }
                },
                Stories =
                {
                    new OwnFollowedStoryItem { StoryId = ownStoryId, Title = "Own Story", IsPublished = true }
                }
            });

        client.GetDeviceFollowsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetDeviceFollowsResponse
            {
                Authors =
                {
                    new DeviceFollowedAuthorItem { AuthorId = sharedAuthorId, Name = "Device Author" }
                },
                Stories =
                {
                    new DeviceFollowedStoryItem { StoryId = deviceStoryId, Title = "Device Story", IsPublished = false }
                }
            });

        var service = CreateService(client);

        var result = await service.GetFollowSnapshotAsync(true, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var snapshot = result.Value!;
        snapshot.Authors.Should().HaveCount(1);
        snapshot.Stories.Should().HaveCount(2);
        snapshot.Authors.Should().Contain(author => author.AuthorId == sharedAuthorId);
        snapshot.Stories.Should().Contain(story => story.StoryId == ownStoryId);
        snapshot.Stories.Should().Contain(story => story.StoryId == deviceStoryId);
    }

    [Fact]
    public async Task MarkNotificationReadAsync_WhenOwnMarkFails_FallsBackToDeviceMark()
    {
        var notificationId = Ulid.NewUlid();
        var client = Substitute.For<IFictionApiClient>();
        client.MarkOwnNotificationReadAsync(Arg.Any<Ulid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<LinkedOfMarkOwnNotificationReadResponse>(CreateApiException(404, "not found")));

        client.MarkDeviceNotificationReadAsync(Arg.Any<Ulid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MarkDeviceNotificationReadResponse());

        var service = CreateService(client);

        var result = await service.MarkNotificationReadAsync(notificationId, true, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await client.Received(1).MarkOwnNotificationReadAsync(notificationId, null, Arg.Any<CancellationToken>());
        await client.Received(1).MarkDeviceNotificationReadAsync(notificationId, DeviceId, null, Arg.Any<CancellationToken>());
    }

    private static NotificationService CreateService(IFictionApiClient client)
        => new(client, () => Task.FromResult<string?>(DeviceId));

    private static OwnNotificationItem CreateOwnNotification(Ulid id, DateTimeOffset deliveredAt, DateTimeOffset? readAt)
        => new()
        {
            NotificationId = id,
            Kind = "story_published",
            Title = $"Own {id}",
            Body = "Body",
            TargetPath = "/stories/test",
            EventOccurredAt = deliveredAt,
            DeliveredAt = deliveredAt,
            ReadAt = readAt,
            AuthorId = Ulid.NewUlid(),
            AuthorName = "Author"
        };

    private static DeviceNotificationItem CreateDeviceNotification(Ulid id, DateTimeOffset deliveredAt, DateTimeOffset? readAt)
        => new()
        {
            NotificationId = id,
            Kind = "story_published",
            Title = $"Device {id}",
            Body = "Body",
            TargetPath = "/stories/test",
            EventOccurredAt = deliveredAt,
            DeliveredAt = deliveredAt,
            ReadAt = readAt,
            AuthorId = Ulid.NewUlid(),
            AuthorName = "Author"
        };

    private static ApiException CreateApiException(int statusCode, string response)
        => new("Request failed.", statusCode, response, new Dictionary<string, IEnumerable<string>>(), null!);

}