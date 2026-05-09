using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Notifications.Domain;

namespace IHFiction.FictionApi.Notifications;

#pragma warning disable CA1515 // Wolverine IncludeType requires public handler/message types.
public sealed record StoryPublishedNotificationRequested(Ulid StoryId);
public sealed record ChapterPublishedNotificationRequested(Ulid ChapterId);

public sealed class NotificationFanoutHandler(FictionDbContext context)
{
    public async Task Handle(
        StoryPublishedNotificationRequested message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var story = await context.Stories
            .Include(candidate => candidate.Owner)
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == message.StoryId && candidate.PublishedAt != null, cancellationToken);

        if (story is null)
        {
            return;
        }

        var notification = await context.Notifications
            .FirstOrDefaultAsync(candidate => candidate.NotificationKey == BuildStoryNotificationKey(story.Id), cancellationToken);

        if (notification is null)
        {
            notification = new NotificationRecord
            {
                NotificationKey = BuildStoryNotificationKey(story.Id),
                Kind = NotificationKinds.StoryPublished,
                Title = Truncate($"{story.Owner.Name} published a new story", 200),
                Body = Truncate($"{story.Title} is now live.", 500),
                TargetPath = $"/stories/{story.Id}",
                EventOccurredAt = story.PublishedAt!.Value,
                AuthorId = story.OwnerId,
                StoryId = story.Id
            };

            context.Notifications.Add(notification);
        }

        var userIds = await context.UserAuthorFollows
            .Where(follow => follow.AuthorId == story.OwnerId && follow.UserId != story.OwnerId)
            .Select(follow => follow.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var deviceIds = await context.DeviceAuthorFollows
            .Where(follow => follow.AuthorId == story.OwnerId)
            .Select(follow => follow.DeviceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await AddMissingUserDeliveriesAsync(notification.Id, userIds, notification.EventOccurredAt, cancellationToken);
        await AddMissingDeviceDeliveriesAsync(notification.Id, deviceIds, notification.EventOccurredAt, cancellationToken);

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task Handle(
        ChapterPublishedNotificationRequested message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var chapter = await context.Chapters
            .Include(candidate => candidate.Owner)
            .Include(candidate => candidate.Story)
            .Include(candidate => candidate.Book)
                .ThenInclude(book => book!.Story)
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == message.ChapterId && candidate.PublishedAt != null, cancellationToken);

        if (chapter is null)
        {
            return;
        }

        var storyId = chapter.StoryId ?? chapter.Book?.StoryId;
        var storyTitle = chapter.Story?.Title ?? chapter.Book?.Story.Title;

        if (storyId is null || string.IsNullOrWhiteSpace(storyTitle))
        {
            return;
        }

        var notification = await context.Notifications
            .FirstOrDefaultAsync(candidate => candidate.NotificationKey == BuildChapterNotificationKey(chapter.Id), cancellationToken);

        if (notification is null)
        {
            notification = new NotificationRecord
            {
                NotificationKey = BuildChapterNotificationKey(chapter.Id),
                Kind = NotificationKinds.ChapterPublished,
                Title = Truncate($"New chapter in {storyTitle}", 200),
                Body = Truncate($"{chapter.Title} by {chapter.Owner.Name} is now live.", 500),
                TargetPath = $"/stories/{storyId}/chapters/{chapter.Id}",
                EventOccurredAt = chapter.PublishedAt!.Value,
                AuthorId = chapter.OwnerId,
                StoryId = storyId,
                ChapterId = chapter.Id
            };

            context.Notifications.Add(notification);
        }

        var authorFollowerUserIds = await context.UserAuthorFollows
            .Where(follow => follow.AuthorId == chapter.OwnerId && follow.UserId != chapter.OwnerId)
            .Select(follow => follow.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var storyFollowerUserIds = await context.UserStoryFollows
            .Where(follow => follow.StoryId == storyId.Value && follow.UserId != chapter.OwnerId)
            .Select(follow => follow.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var authorFollowerDeviceIds = await context.DeviceAuthorFollows
            .Where(follow => follow.AuthorId == chapter.OwnerId)
            .Select(follow => follow.DeviceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var storyFollowerDeviceIds = await context.DeviceStoryFollows
            .Where(follow => follow.StoryId == storyId.Value)
            .Select(follow => follow.DeviceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await AddMissingUserDeliveriesAsync(
            notification.Id,
            authorFollowerUserIds.Concat(storyFollowerUserIds).Distinct(),
            notification.EventOccurredAt,
            cancellationToken);

        await AddMissingDeviceDeliveriesAsync(
            notification.Id,
            authorFollowerDeviceIds.Concat(storyFollowerDeviceIds).Distinct(),
            notification.EventOccurredAt,
            cancellationToken);

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task AddMissingUserDeliveriesAsync(
        Ulid notificationId,
        IEnumerable<Ulid> userIds,
        DateTime deliveredAt,
        CancellationToken cancellationToken)
    {
        var recipientIds = userIds.Distinct().ToArray();

        if (recipientIds.Length == 0)
        {
            return;
        }

        var existingRecipientIds = await context.UserNotificationDeliveries
            .Where(delivery => delivery.NotificationId == notificationId && recipientIds.Contains(delivery.UserId))
            .Select(delivery => delivery.UserId)
            .ToListAsync(cancellationToken);

        foreach (var userId in recipientIds.Except(existingRecipientIds))
        {
            context.UserNotificationDeliveries.Add(new UserNotificationDelivery
            {
                NotificationId = notificationId,
                UserId = userId,
                DeliveredAt = deliveredAt
            });
        }
    }

    private async Task AddMissingDeviceDeliveriesAsync(
        Ulid notificationId,
        IEnumerable<string> deviceIds,
        DateTime deliveredAt,
        CancellationToken cancellationToken)
    {
        var recipientIds = deviceIds
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (recipientIds.Length == 0)
        {
            return;
        }

        var existingRecipientIds = await context.DeviceNotificationDeliveries
            .Where(delivery => delivery.NotificationId == notificationId && recipientIds.Contains(delivery.DeviceId))
            .Select(delivery => delivery.DeviceId)
            .ToListAsync(cancellationToken);

        foreach (var deviceId in recipientIds.Except(existingRecipientIds, StringComparer.Ordinal))
        {
            context.DeviceNotificationDeliveries.Add(new DeviceNotificationDelivery
            {
                NotificationId = notificationId,
                DeviceId = deviceId,
                DeliveredAt = deliveredAt
            });
        }
    }

    private static string BuildStoryNotificationKey(Ulid storyId) => $"{NotificationKinds.StoryPublished}:{storyId}";

    private static string BuildChapterNotificationKey(Ulid chapterId) => $"{NotificationKinds.ChapterPublished}:{chapterId}";

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }
}
#pragma warning restore CA1515