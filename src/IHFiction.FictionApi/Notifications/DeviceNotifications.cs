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

internal sealed class GetDeviceFollows(FictionDbContext context) : IUseCase, INameEndpoint<GetDeviceFollows>
{
    internal static readonly DomainError InvalidDeviceId =
        new("Device.InvalidIdentifier", "A valid device identifier is required.");

    internal sealed record GetDeviceFollowsQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetDeviceFollowsResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record DeviceFollowedAuthorItem(Ulid AuthorId, string Name);
    internal sealed record DeviceFollowedStoryItem(Ulid StoryId, string Title, bool IsPublished);
    internal sealed record GetDeviceFollowsResponse(
        IEnumerable<DeviceFollowedAuthorItem> Authors,
        IEnumerable<DeviceFollowedStoryItem> Stories);

    public async Task<Result<GetDeviceFollowsResponse>> HandleAsync(
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceIdHeader.IsValid(deviceId)) return InvalidDeviceId;

        var authors = await context.DeviceAuthorFollows
            .AsNoTracking()
            .Where(follow => follow.DeviceId == deviceId)
            .Join(
                context.Authors.AsNoTracking(),
                follow => follow.AuthorId,
                author => author.Id,
                (follow, author) => new { author.Id, author.Name })
            .OrderBy(item => item.Name)
            .Select(item => new DeviceFollowedAuthorItem(item.Id, item.Name))
            .ToListAsync(cancellationToken);

        var stories = await context.DeviceStoryFollows
            .AsNoTracking()
            .Where(follow => follow.DeviceId == deviceId)
            .Join(
                context.Stories.AsNoTracking(),
                follow => follow.StoryId,
                story => story.Id,
                (follow, story) => new { story.Id, story.Title, IsPublished = story.PublishedAt != null })
            .OrderBy(item => item.Title)
            .Select(item => new DeviceFollowedStoryItem(item.Id, item.Title, item.IsPublished))
            .ToListAsync(cancellationToken);

        return new GetDeviceFollowsResponse(authors, stories);
    }

    public static string EndpointName => nameof(GetDeviceFollows);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("device/follows", async (
                [FromHeader(Name = DeviceIdHeader.Name)] string? deviceId,
                [AsParameters] GetDeviceFollowsQuery query,
                GetDeviceFollows useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(deviceId, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Get Device Follows")
            .WithDescription("Lists the authors and stories followed by an anonymous browser or installed PWA device.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false)
            .Produces<GetDeviceFollowsResponse>();
        }
    }
}

internal sealed class GetDeviceNotifications(FictionDbContext context) : IUseCase, INameEndpoint<GetDeviceNotifications>
{
    internal static readonly DomainError InvalidDeviceId =
        new("Device.InvalidIdentifier", "A valid device identifier is required.");

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
        if (!DeviceIdHeader.IsValid(deviceId)) return InvalidDeviceId;

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

internal sealed class MarkDeviceNotificationRead(
    FictionDbContext context,
    TimeProvider timeProvider) : IUseCase, INameEndpoint<MarkDeviceNotificationRead>
{
    internal static readonly DomainError InvalidDeviceId =
        new("Device.InvalidIdentifier", "A valid device identifier is required.");

    internal static readonly DomainError NotificationNotFound =
        new("Notification.NotFound", "Notification not found.");

    internal sealed record MarkDeviceNotificationReadQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<MarkDeviceNotificationReadResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record MarkDeviceNotificationReadResponse(Ulid NotificationId, DateTime ReadAt);

    public async Task<Result<MarkDeviceNotificationReadResponse>> HandleAsync(
        Ulid id,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceIdHeader.IsValid(deviceId)) return InvalidDeviceId;

        var delivery = await context.DeviceNotificationDeliveries
            .FirstOrDefaultAsync(candidate => candidate.DeviceId == deviceId && candidate.NotificationId == id, cancellationToken);

        if (delivery is null) return NotificationNotFound;

        delivery.ReadAt ??= timeProvider.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(cancellationToken);

        return new MarkDeviceNotificationReadResponse(id, delivery.ReadAt.Value);
    }

    public static string EndpointName => nameof(MarkDeviceNotificationRead);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("device/notifications/{id:ulid}/read", async (
                [FromRoute] Ulid id,
                [FromHeader(Name = DeviceIdHeader.Name)] string? deviceId,
                [AsParameters] MarkDeviceNotificationReadQuery query,
                MarkDeviceNotificationRead useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, deviceId, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Mark Device Notification Read")
            .WithDescription("Marks a notification as read for an anonymous browser or installed PWA device.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false)
            .Produces<MarkDeviceNotificationReadResponse>();
        }
    }
}