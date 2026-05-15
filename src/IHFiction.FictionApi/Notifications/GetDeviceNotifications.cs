using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Notifications;

internal sealed class GetDeviceNotifications(FictionDbContext context) : IUseCase, INameEndpoint<GetDeviceNotifications>
{
    internal sealed record GetDeviceNotificationsQuery(
        [property: Range(1, 200, ErrorMessage = "Limit must be between 1 and 200.")]
        int Limit = 50,
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetDeviceNotificationsResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record DeviceNotificationItem(
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

    internal sealed record GetDeviceNotificationsResponse(
        IEnumerable<DeviceNotificationItem> Items,
        int UnreadCount);

    public async Task<Result<GetDeviceNotificationsResponse>> HandleAsync(
        string? deviceId,
        GetDeviceNotificationsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceIdHeader.IsValid(deviceId)) return CommonErrors.Device.InvalidIdentifier;

        var deliveries = await context.DeviceNotificationDeliveries
            .Where(delivery => delivery.DeviceId == deviceId)
            .Include(delivery => delivery.Notification)
                .ThenInclude(notification => notification.Author)
            .Include(delivery => delivery.Notification)
                .ThenInclude(notification => notification.Story)
            .Include(delivery => delivery.Notification)
                .ThenInclude(notification => notification.Chapter)
            .AsNoTracking()
            .OrderByDescending(delivery => delivery.DeliveredAt)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);

        var unreadCount = await context.DeviceNotificationDeliveries
            .CountAsync(delivery => delivery.DeviceId == deviceId && delivery.ReadAt == null, cancellationToken);

        return new GetDeviceNotificationsResponse(
            deliveries.Select(delivery => new DeviceNotificationItem(
                delivery.NotificationId,
                delivery.Notification.Kind,
                delivery.Notification.Title,
                delivery.Notification.Body,
                delivery.Notification.TargetPath,
                delivery.Notification.EventOccurredAt,
                delivery.DeliveredAt,
                delivery.ReadAt,
                delivery.Notification.AuthorId,
                delivery.Notification.Author.Name,
                delivery.Notification.StoryId,
                delivery.Notification.Story?.Title,
                delivery.Notification.ChapterId,
                delivery.Notification.Chapter?.Title)),
            unreadCount);
    }

    public static string EndpointName => nameof(GetDeviceNotifications);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("device/notifications", async (
                [FromHeader(Name = DeviceIdHeader.Name)] string? deviceId,
                [AsParameters] GetDeviceNotificationsQuery query,
                GetDeviceNotifications useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(deviceId, query, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Get Device Notification Inbox")
            .WithDescription("Lists recent notifications for an anonymous browser or installed PWA device and includes the unread count.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false)
            .Produces<GetDeviceNotificationsResponse>();
        }
    }
}
