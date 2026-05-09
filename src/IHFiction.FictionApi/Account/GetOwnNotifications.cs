using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Account;

internal sealed class GetOwnNotifications(
    FictionDbContext context,
    UserService userService) : IUseCase, INameEndpoint<GetOwnNotifications>
{
    internal sealed record GetOwnNotificationsQuery(
        [property: Range(1, 200, ErrorMessage = "Limit must be between 1 and 200.")]
        int Limit = 50,
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetOwnNotificationsResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record OwnNotificationItem(
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

    internal sealed record GetOwnNotificationsResponse(
        IEnumerable<OwnNotificationItem> Items,
        int UnreadCount);

    public async Task<Result<GetOwnNotificationsResponse>> HandleAsync(
        GetOwnNotificationsQuery query,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var userResult = await userService.GetUserAsync(claimsPrincipal, cancellationToken);
        if (userResult.IsFailure) return userResult.DomainError;

        var user = userResult.Value;

        var deliveries = await context.UserNotificationDeliveries
            .Where(delivery => delivery.UserId == user.Id)
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

        var unreadCount = await context.UserNotificationDeliveries
            .CountAsync(delivery => delivery.UserId == user.Id && delivery.ReadAt == null, cancellationToken);

        return new GetOwnNotificationsResponse(
            deliveries.Select(delivery => new OwnNotificationItem(
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

    public static string EndpointName => nameof(GetOwnNotifications);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("me/notifications", async (
                [AsParameters] GetOwnNotificationsQuery query,
                GetOwnNotifications useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(query, claimsPrincipal, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Get Notification Inbox")
            .WithDescription("Lists recent notifications for the currently authenticated user and includes the unread count.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization()
            .WithStandardResponses(conflict: false, notFound: false)
            .Produces<GetOwnNotificationsResponse>();
        }
    }
}