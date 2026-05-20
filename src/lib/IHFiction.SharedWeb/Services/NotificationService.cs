using System.Globalization;

using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb.Extensions;

namespace IHFiction.SharedWeb.Services;

public sealed class NotificationService
{
    private const string DeviceIdStorageKey = "notifications:device-id";
    private readonly IFictionApiClient _client;
    private readonly BrowserProtectedStorageService? _storage;
    private readonly Func<Task<string?>> getOrCreateDeviceIdAsync;

    public NotificationService(IFictionApiClient client, BrowserProtectedStorageService storage)
    {
        _client = client;
        _storage = storage;
        getOrCreateDeviceIdAsync = GetOrCreateStoredDeviceIdAsync;
    }

    public NotificationService(IFictionApiClient client, Func<Task<string?>> getOrCreateDeviceIdAsync)
    {
        _client = client;
        this.getOrCreateDeviceIdAsync = getOrCreateDeviceIdAsync;
    }

    public async Task<Result<FollowSnapshot>> GetFollowSnapshotAsync(bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        if (isAuthenticated)
        {
            var ownResult = await _client.GetOwnFollowsAsync(null, cancellationToken).HandleApiException();
            var authDeviceId = await GetOrCreateDeviceIdAsync();
            var authDeviceResult = await _client.GetDeviceFollowsAsync(authDeviceId, null, cancellationToken).HandleApiException();

            if (ownResult.IsFailure && authDeviceResult.IsFailure)
            {
                return ownResult.DomainError;
            }

            var ownSnapshot = ownResult.IsFailure
                ? new FollowSnapshot([], [])
                : MapFollowSnapshot(ownResult.Value);

            var deviceSnapshot = authDeviceResult.IsFailure
                ? new FollowSnapshot([], [])
                : MapFollowSnapshot(authDeviceResult.Value);

            return MergeFollowSnapshot(ownSnapshot, deviceSnapshot);
        }

        var deviceId = await GetOrCreateDeviceIdAsync();
        var deviceResult = await _client.GetDeviceFollowsAsync(deviceId, null, cancellationToken).HandleApiException();
        return deviceResult.IsFailure
            ? deviceResult.DomainError
            : MapFollowSnapshot(deviceResult.Value);
    }

    public async Task<Result<NotificationInbox>> GetNotificationsAsync(bool isAuthenticated, int limit = 50, CancellationToken cancellationToken = default)
    {
        if (isAuthenticated)
        {
            var ownResult = await _client.GetOwnNotificationsAsync(limit, null, cancellationToken).HandleApiException();
            var authDeviceId = await GetOrCreateDeviceIdAsync();
            var authDeviceResult = await _client.GetDeviceNotificationsAsync(authDeviceId, limit, null, cancellationToken).HandleApiException();

            if (ownResult.IsFailure && authDeviceResult.IsFailure)
            {
                return ownResult.DomainError;
            }

            var ownInbox = ownResult.IsFailure
                ? new NotificationInbox([], 0)
                : MapNotificationInbox(ownResult.Value);

            var deviceInbox = authDeviceResult.IsFailure
                ? new NotificationInbox([], 0)
                : MapNotificationInbox(authDeviceResult.Value);

            return MergeNotificationInbox(ownInbox, deviceInbox, limit);
        }

        var deviceId = await GetOrCreateDeviceIdAsync();
        var deviceResult = await _client.GetDeviceNotificationsAsync(deviceId, limit, null, cancellationToken).HandleApiException();
        return deviceResult.IsFailure
            ? deviceResult.DomainError
            : MapNotificationInbox(deviceResult.Value);
    }

    public async Task<Result> FollowAuthorAsync(Ulid authorId, bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        return isAuthenticated
            ? await _client.FollowAuthorAsync(authorId, null, cancellationToken).HandleApiException()
            : await _client.FollowAuthorForDeviceAsync(authorId, await GetOrCreateDeviceIdAsync(), null, cancellationToken).HandleApiException();
    }

    public async Task<Result> UnfollowAuthorAsync(Ulid authorId, bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        return isAuthenticated
            ? await _client.UnfollowAuthorAsync(authorId, null, cancellationToken).HandleApiException()
            : await _client.UnfollowAuthorForDeviceAsync(authorId, await GetOrCreateDeviceIdAsync(), null, cancellationToken).HandleApiException();
    }

    public async Task<Result> FollowStoryAsync(Ulid storyId, bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        return isAuthenticated
            ? await _client.FollowStoryAsync(storyId, null, cancellationToken).HandleApiException()
            : await _client.FollowStoryForDeviceAsync(storyId, await GetOrCreateDeviceIdAsync(), null, cancellationToken).HandleApiException();
    }

    public async Task<Result> UnfollowStoryAsync(Ulid storyId, bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        return isAuthenticated
            ? await _client.UnfollowStoryAsync(storyId, null, cancellationToken).HandleApiException()
            : await _client.UnfollowStoryForDeviceAsync(storyId, await GetOrCreateDeviceIdAsync(), null, cancellationToken).HandleApiException();
    }

    public async Task<Result> MarkNotificationReadAsync(Ulid notificationId, bool isAuthenticated, CancellationToken cancellationToken = default)
    {
        if (!isAuthenticated)
        {
            return await _client.MarkDeviceNotificationReadAsync(notificationId, await GetOrCreateDeviceIdAsync(), null, cancellationToken).HandleApiException();
        }

        var ownResult = await _client.MarkOwnNotificationReadAsync(notificationId, null, cancellationToken).HandleApiException();
        if (ownResult.IsSuccess)
        {
            return ownResult;
        }

        var deviceId = await GetOrCreateDeviceIdAsync();
        var deviceResult = await _client.MarkDeviceNotificationReadAsync(notificationId, deviceId, null, cancellationToken).HandleApiException();

        return deviceResult.IsSuccess
            ? deviceResult
            : ownResult;
    }

    public async Task<Result> RegisterPushSubscriptionAsync(
        BrowserPushSubscription subscription,
        bool isAuthenticated,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return isAuthenticated
            ? await _client.RegisterOwnPushSubscriptionAsync(subscription, cancellationToken).HandleApiException()
            : await _client.RegisterDevicePushSubscriptionAsync(subscription, await GetOrCreateDeviceIdAsync(), cancellationToken).HandleApiException();
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
        return await getOrCreateDeviceIdAsync();
    }

    private async Task<string?> GetOrCreateStoredDeviceIdAsync()
    {
        var existing = await _storage!.GetAsync<string>(DeviceIdStorageKey);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var created = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        await _storage.SetAsync(DeviceIdStorageKey, created);
        return created;
    }

    private static FollowSnapshot MapFollowSnapshot(GetOwnFollowsResponse response) =>
        new(
            response.Authors.Select(author => new FollowedAuthor(ParseUlid(author.AuthorId), author.Name)).ToList(),
            response.Stories.Select(story => new FollowedStory(ParseUlid(story.StoryId), story.Title, story.IsPublished)).ToList());

    private static FollowSnapshot MapFollowSnapshot(GetDeviceFollowsResponse response) =>
        new(
            response.Authors.Select(author => new FollowedAuthor(ParseUlid(author.AuthorId), author.Name)).ToList(),
            response.Stories.Select(story => new FollowedStory(ParseUlid(story.StoryId), story.Title, story.IsPublished)).ToList());

    private static NotificationInbox MapNotificationInbox(GetOwnNotificationsResponse response) =>
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

    private static NotificationInbox MapNotificationInbox(GetDeviceNotificationsResponse response) =>
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

    private static FollowSnapshot MergeFollowSnapshot(FollowSnapshot own, FollowSnapshot device)
    {
        var authors = own.Authors
            .Concat(device.Authors)
            .DistinctBy(author => author.AuthorId)
            .ToList();

        var stories = own.Stories
            .Concat(device.Stories)
            .DistinctBy(story => story.StoryId)
            .ToList();

        return new FollowSnapshot(authors, stories);
    }

    private static NotificationInbox MergeNotificationInbox(NotificationInbox own, NotificationInbox device, int limit)
    {
        var items = own.Items
            .Concat(device.Items)
            .GroupBy(item => item.NotificationId)
            .Select(group =>
            {
                var ordered = group
                    .OrderByDescending(item => item.DeliveredAt)
                    .ToList();

                return ordered.FirstOrDefault(item => item.ReadAt is null) ?? ordered[0];
            })
            .OrderByDescending(item => item.DeliveredAt)
            .Take(limit)
            .ToList();

        var unreadCount = items.Count(item => item.ReadAt is null);
        return new NotificationInbox(items, unreadCount);
    }

    private static Ulid ParseUlid(Ulid? value) =>
        value ?? throw new InvalidOperationException("Expected notification payload to include a required identifier.");
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

public sealed record PushSubscriptionKeys(
    string P256DH,
    string Auth
);

public sealed record PushSubscription(
    string Endpoint,
    double? ExpirationTime,
    PushSubscriptionKeys? Keys);

public sealed record BrowserPushSubscription(
    PushSubscription Subscription,
    string? UserAgent)
{
    public static RegisterDevicePushSubscriptionBody ToRegisterDevicePushSubscriptionBody(BrowserPushSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return new()
        {
            Endpoint = subscription.Subscription.Endpoint,
            P256dhKey = subscription.Subscription.Keys?.P256DH,
            AuthKey = subscription.Subscription.Keys?.Auth,
            ExpiresAt = subscription.Subscription.ExpirationTime.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds((long)subscription.Subscription.ExpirationTime) : null,
            UserAgent = subscription.UserAgent
        };
    }

    public static implicit operator RegisterDevicePushSubscriptionBody(BrowserPushSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        return ToRegisterDevicePushSubscriptionBody(subscription);
    }

    public static RegisterOwnPushSubscriptionBody ToRegisterOwnPushSubscriptionBody(BrowserPushSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return new()
        {
            Endpoint = subscription.Subscription.Endpoint,
            P256dhKey = subscription.Subscription.Keys?.P256DH,
            AuthKey = subscription.Subscription.Keys?.Auth,
            ExpiresAt = subscription.Subscription.ExpirationTime.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds((long)subscription.Subscription.ExpirationTime) : null,
            UserAgent = subscription.UserAgent
        };
    }

    public static implicit operator RegisterOwnPushSubscriptionBody(BrowserPushSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        return ToRegisterOwnPushSubscriptionBody(subscription);
    }
}
