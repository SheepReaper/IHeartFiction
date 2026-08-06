using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Notifications.Domain;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Notifications;
using IHFiction.SharedKernel.Notifications;

namespace IHFiction.UnitTests.Notifications;

public class StoryCompletedNotificationTests
{
    [Fact]
    public async Task Handle_CompletePublishedStory_DeliversOnceToStoryFollowers()
    {
        await using var context = CreateContext();
        var author = new Author { Id = Ulid.NewUlid(), UserId = Guid.NewGuid(), Name = "Author" };
        var follower = User.FromUserId(Guid.NewGuid(), "Reader");
        var story = new Story
        {
            Id = Ulid.NewUlid(),
            Title = "Finished Story",
            Description = "A completed story for notification testing.",
            OwnerId = author.Id,
            PublishedAt = DateTime.UtcNow.AddDays(-1),
            CompletionStatus = StoryCompletionStatus.Complete
        };

        context.AddRange(author, follower, story);
        context.UserStoryFollows.Add(new UserStoryFollow { UserId = follower.Id, StoryId = story.Id });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = CreateHandler(context);
        var message = new StoryCompletedNotificationRequested(story.Id);

        await handler.Handle(message, TestContext.Current.CancellationToken);
        await handler.Handle(message, TestContext.Current.CancellationToken);

        var notification = await context.Notifications.SingleAsync(TestContext.Current.CancellationToken);
        notification.Kind.Should().Be(NotificationKinds.StoryCompleted);
        notification.NotificationKey.Should().Be($"{NotificationKinds.StoryCompleted}:{story.Id}");
        (await context.UserNotificationDeliveries.ToListAsync(TestContext.Current.CancellationToken))
            .Should().ContainSingle(delivery => delivery.UserId == follower.Id && delivery.NotificationId == notification.Id);
    }

    [Fact]
    public async Task Handle_InProgressStory_DoesNotCreateNotification()
    {
        await using var context = CreateContext();
        var author = new Author { Id = Ulid.NewUlid(), UserId = Guid.NewGuid(), Name = "Author" };
        var story = new Story
        {
            Id = Ulid.NewUlid(),
            Title = "Ongoing Story",
            Description = "An in-progress story for notification testing.",
            OwnerId = author.Id,
            PublishedAt = DateTime.UtcNow,
            CompletionStatus = StoryCompletionStatus.InProgress
        };

        context.AddRange(author, story);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await CreateHandler(context).Handle(
            new StoryCompletedNotificationRequested(story.Id),
            TestContext.Current.CancellationToken);

        (await context.Notifications.ToListAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    private static NotificationFanoutHandler CreateHandler(FictionDbContext context) => new(
        context,
        Options.Create(new WebPushOptions { Subject = "", PublicKey = "", PrivateKey = "" }),
        NullLogger<NotificationFanoutHandler>.Instance);

    private static FictionDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FictionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FictionDbContext(options);
    }
}
