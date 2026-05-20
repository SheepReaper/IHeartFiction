using Microsoft.EntityFrameworkCore;

using FluentAssertions;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Notifications.Domain;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Authors;

namespace IHFiction.UnitTests.Notifications;

public class DeviceAuthorUnfollowTests
{
    [Fact]
    public async Task HandleAsync_RemovesDevicePushSubscription_WhenLastDeviceFollowIsRemoved()
    {
        await using var context = CreateContext();
        var author = new Author { Id = Ulid.NewUlid(), Name = "Author" };
        const string deviceId = "device-1";

        context.Authors.Add(author);
        context.DeviceAuthorFollows.Add(new DeviceAuthorFollow { DeviceId = deviceId, AuthorId = author.Id });
        context.DevicePushSubscriptions.Add(new DevicePushSubscription
        {
            DeviceId = deviceId,
            Endpoint = "https://push.example/subscription/1",
            P256dhKey = "p256dh",
            AuthKey = "auth"
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new UnfollowAuthorForDevice(context);

        var result = await useCase.HandleAsync(author.Id, deviceId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        (await context.DeviceAuthorFollows.ToListAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        (await context.DevicePushSubscriptions.ToListAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_KeepsDevicePushSubscription_WhenDeviceStillFollowsAStory()
    {
        await using var context = CreateContext();
        var author = new Author { Id = Ulid.NewUlid(), Name = "Author" };
        var story = new Story
        {
            Id = Ulid.NewUlid(),
            Title = "Story",
            Description = "A story this device still follows.",
            OwnerId = author.Id
        };
        const string deviceId = "device-1";

        context.Authors.Add(author);
        context.Stories.Add(story);
        context.DeviceAuthorFollows.Add(new DeviceAuthorFollow { DeviceId = deviceId, AuthorId = author.Id });
        context.DeviceStoryFollows.Add(new DeviceStoryFollow { DeviceId = deviceId, StoryId = story.Id });
        context.DevicePushSubscriptions.Add(new DevicePushSubscription
        {
            DeviceId = deviceId,
            Endpoint = "https://push.example/subscription/1",
            P256dhKey = "p256dh",
            AuthKey = "auth"
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new UnfollowAuthorForDevice(context);

        var result = await useCase.HandleAsync(author.Id, deviceId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        (await context.DeviceAuthorFollows.ToListAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        (await context.DevicePushSubscriptions.ToListAsync(TestContext.Current.CancellationToken)).Should().ContainSingle();
    }

    private static FictionDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FictionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FictionDbContext(options);
    }
}
