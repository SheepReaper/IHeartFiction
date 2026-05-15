using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;

namespace IHFiction.FictionApi.Notifications;

internal static class DevicePushSubscriptionCleanup
{
    public static async Task RemoveIfDeviceHasNoFollowsAsync(
        FictionDbContext context,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        var hasAuthorFollows = await context.DeviceAuthorFollows
            .AnyAsync(follow => follow.DeviceId == deviceId, cancellationToken);

        if (hasAuthorFollows)
        {
            return;
        }

        var hasStoryFollows = await context.DeviceStoryFollows
            .AnyAsync(follow => follow.DeviceId == deviceId, cancellationToken);

        if (hasStoryFollows)
        {
            return;
        }

        var subscriptions = await context.DevicePushSubscriptions
            .Where(subscription => subscription.DeviceId == deviceId)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count is 0)
        {
            return;
        }

        context.DevicePushSubscriptions.RemoveRange(subscriptions);
        await context.SaveChangesAsync(cancellationToken);
    }
}
