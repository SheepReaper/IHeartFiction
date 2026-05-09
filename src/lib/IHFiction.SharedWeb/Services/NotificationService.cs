using System.Globalization;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Services;

public sealed class NotificationService(FictionApiClient client, BrowserProtectedStorageService storage)
{
    private const string DeviceIdStorageKey = "notifications:device-id";

    public async Task<Result<FollowSnapshot>> GetFollowSnapshotAsync(bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        if (isAuthenticated)
        {
            var result = await client.GetOwnFollowsAsync(null, cancellationToken).HandleApiException();
            return result.IsFailure
                ? result.DomainError
                : MapFollowSnapshot(result.Value);
        }

        var deviceId = await GetOrCreateDeviceIdAsync();
        var deviceResult = await client.GetDeviceFollowsAsync(deviceId, null, cancellationToken).HandleApiException();
        return deviceResult.IsFailure
            ? deviceResult.DomainError
            : MapFollowSnapshot(deviceResult.Value);
    }

    public async Task<Result<NotificationInbox>> GetNotificationsAsync(bool isAuthenticated, int limit = 50, CancellationToken cancellationToken = default)
    {
        if (isAuthenticated)
        {
            var result = await client.GetOwnNotificationsAsync(limit, null, cancellationToken).HandleApiException();
            return result.IsFailure
                ? result.DomainError
                : MapNotificationInbox(result.Value);
        }

        var deviceId = await GetOrCreateDeviceIdAsync();
        var deviceResult = await client.GetDeviceNotificationsAsync(deviceId, limit, null, cancellationToken).HandleApiException();
        return deviceResult.IsFailure
            ? deviceResult.DomainError
            : MapNotificationInbox(deviceResult.Value);
    }

    public async Task<Result> FollowAuthorAsync(Ulid authorId, bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        return isAuthenticated
            ? await client.FollowAuthorAsync(authorId, null, cancellationToken).HandleApiException()
            : await client.FollowAuthorForDeviceAsync(authorId, await GetOrCreateDeviceIdAsync(), null, cancellationToken).HandleApiException();
    }

    public async Task<Result> UnfollowAuthorAsync(Ulid authorId, bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        return isAuthenticated
            ? await client.UnfollowAuthorAsync(authorId, null, cancellationToken).HandleApiException()
            : await client.UnfollowAuthorForDeviceAsync(authorId, await GetOrCreateDeviceIdAsync(), null, cancellationToken).HandleApiException();
    }

    public async Task<Result> FollowStoryAsync(Ulid storyId, bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        return isAuthenticated
            ? await client.FollowStoryAsync(storyId, null, cancellationToken).HandleApiException()
            : await client.FollowStoryForDeviceAsync(storyId, await GetOrCreateDeviceIdAsync(), null, cancellationToken).HandleApiException();
    }

    public async Task<Result> UnfollowStoryAsync(Ulid storyId, bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        return isAuthenticated
            ? await client.UnfollowStoryAsync(storyId, null, cancellationToken).HandleApiException()
            : await client.UnfollowStoryForDeviceAsync(storyId, await GetOrCreateDeviceIdAsync(), null, cancellationToken).HandleApiException();
    }

    public async Task<Result> MarkNotificationReadAsync(Ulid notificationId, bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        return isAuthenticated
            ? await client.MarkOwnNotificationReadAsync(notificationId, null, cancellationToken).HandleApiException()
            : await client.MarkDeviceNotificationReadAsync(notificationId, await GetOrCreateDeviceIdAsync(), null, cancellationToken).HandleApiException();
    }

    public async Task<Result> RegisterPushSubscriptionAsync(
        BrowserPushSubscription subscription,
        bool isAuthenticated,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return isAuthenticated
            ? await client.RegisterOwnPushSubscriptionAsync(MapOwnPushSubscription(subscription), cancellationToken).HandleApiException()
            : await client.RegisterDevicePushSubscriptionAsync(MapDevicePushSubscription(subscription), await GetOrCreateDeviceIdAsync(), cancellationToken).HandleApiException();
    }

    public async Task<Result<bool>> IsAuthorFollowedAsync(Ulid authorId, bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        var followsResult = await GetFollowSnapshotAsync(isAuthenticated, cancellationToken);
        if (followsResult.IsFailure)
        {
            return followsResult.DomainError;
        }

        return followsResult.Value.Authors.Any(author => author.AuthorId == authorId);
    }

    public async Task<Result<bool>> IsStoryFollowedAsync(Ulid storyId, bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        var followsResult = await GetFollowSnapshotAsync(isAuthenticated, cancellationToken);
        if (followsResult.IsFailure)
        {
            return followsResult.DomainError;
        }

        return followsResult.Value.Stories.Any(story => story.StoryId == storyId);
    }

    private async Task<string?> GetOrCreateDeviceIdAsync()
    {
        var existing = await storage.GetAsync<string>(DeviceIdStorageKey);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var created = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        await storage.SetAsync(DeviceIdStorageKey, created);
        return created;
    }

    private static FollowSnapshot MapFollowSnapshot(global::IHFiction.SharedWeb.GetOwnFollowsResponse response) =>
        new(
            response.Authors.Select(author => new FollowedAuthor(ParseUlid(author.AuthorId), author.Name)).ToList(),
            response.Stories.Select(story => new FollowedStory(ParseUlid(story.StoryId), story.Title, story.IsPublished)).ToList());

    private static FollowSnapshot MapFollowSnapshot(global::IHFiction.SharedWeb.GetDeviceFollowsResponse response) =>
        new(
            response.Authors.Select(author => new FollowedAuthor(ParseUlid(author.AuthorId), author.Name)).ToList(),
            response.Stories.Select(story => new FollowedStory(ParseUlid(story.StoryId), story.Title, story.IsPublished)).ToList());

    private static NotificationInbox MapNotificationInbox(global::IHFiction.SharedWeb.GetOwnNotificationsResponse response) =>
        new(
            response.Items.Select(item => new NotificationListItem(
                ParseUlid(item.NotificationId),
                item.Kind,
                item.Title,
                item.Body,
                item.TargetPath,
                item.EventOccurredAt.UtcDateTime,
                item.DeliveredAt.UtcDateTime,
                item.ReadAt?.UtcDateTime,
                ParseUlid(item.AuthorId),
                item.AuthorName,
                item.StoryId,
                item.StoryTitle,
                item.ChapterId,
                item.ChapterTitle)).ToList(),
            response.UnreadCount);

    private static NotificationInbox MapNotificationInbox(global::IHFiction.SharedWeb.GetDeviceNotificationsResponse response) =>
        new(
            [.. response.Items.Select(item => new NotificationListItem(
                ParseUlid(item.NotificationId),
                item.Kind,
                item.Title,
                item.Body,
                item.TargetPath,
                item.EventOccurredAt.UtcDateTime,
                item.DeliveredAt.UtcDateTime,
                item.ReadAt?.UtcDateTime,
                ParseUlid(item.AuthorId),
                item.AuthorName,
                item.StoryId,
                item.StoryTitle,
                item.ChapterId,
                item.ChapterTitle))],
            response.UnreadCount);

    private static Ulid ParseUlid(Ulid value) => value;

    private static RegisterOwnPushSubscriptionBody MapOwnPushSubscription(BrowserPushSubscription subscription) =>
        new()
        {
            Endpoint = subscription.Endpoint,
            P256dhKey = subscription.P256dhKey,
            AuthKey = subscription.AuthKey,
            ExpiresAt = subscription.ExpiresAt,
            UserAgent = subscription.UserAgent
        };

    private static RegisterDevicePushSubscriptionBody MapDevicePushSubscription(BrowserPushSubscription subscription) =>
        new()
        {
            Endpoint = subscription.Endpoint,
            P256dhKey = subscription.P256dhKey,
            AuthKey = subscription.AuthKey,
            ExpiresAt = subscription.ExpiresAt,
            UserAgent = subscription.UserAgent
        };
}

public sealed record FollowSnapshot(
    IReadOnlyList<FollowedAuthor> Authors,
    IReadOnlyList<FollowedStory> Stories);

public sealed record FollowedAuthor(Ulid AuthorId, string Name);

public sealed record FollowedStory(Ulid StoryId, string Title, bool IsPublished);

public sealed record NotificationInbox(
    IReadOnlyList<NotificationListItem> Items,
    int UnreadCount);

public sealed record NotificationListItem(
    Ulid NotificationId,
    string Kind,
    string Title,
    string Body,
    string TargetPath,
    DateTime EventOccurredAt,
    DateTime DeliveredAt,
    DateTime? ReadAt,
    Ulid AuthorId,
    string AuthorName,
    Ulid? StoryId,
    string? StoryTitle,
    Ulid? ChapterId,
    string? ChapterTitle);

public sealed record BrowserPushSubscription(
    string Endpoint,
    string P256dhKey,
    string AuthKey,
    DateTime? ExpiresAt,
    string? UserAgent);
