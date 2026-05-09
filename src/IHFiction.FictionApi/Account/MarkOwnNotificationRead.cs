using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Account;

internal sealed class MarkOwnNotificationRead(
    FictionDbContext context,
    UserService userService,
    TimeProvider timeProvider) : IUseCase, INameEndpoint<MarkOwnNotificationRead>
{
    internal static readonly DomainError NotificationNotFound =
        new("Notification.NotFound", "Notification not found.");

    internal sealed record MarkOwnNotificationReadQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<MarkOwnNotificationReadResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record MarkOwnNotificationReadResponse(Ulid NotificationId, DateTime ReadAt);

    public async Task<Result<MarkOwnNotificationReadResponse>> HandleAsync(
        Ulid id,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var userResult = await userService.GetUserAsync(claimsPrincipal, cancellationToken);
        if (userResult.IsFailure) return userResult.DomainError;

        var delivery = await context.UserNotificationDeliveries
            .FirstOrDefaultAsync(candidate => candidate.UserId == userResult.Value.Id && candidate.NotificationId == id, cancellationToken);

        if (delivery is null) return NotificationNotFound;

        delivery.ReadAt ??= timeProvider.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(cancellationToken);

        return new MarkOwnNotificationReadResponse(id, delivery.ReadAt.Value);
    }

    public static string EndpointName => nameof(MarkOwnNotificationRead);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("me/notifications/{id:ulid}/read", async (
                [FromRoute] Ulid id,
                [AsParameters] MarkOwnNotificationReadQuery query,
                MarkOwnNotificationRead useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Mark Notification Read")
            .WithDescription("Marks a notification as read for the currently authenticated user.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization()
            .WithStandardResponses(conflict: false)
            .Produces<Linked<MarkOwnNotificationReadResponse>>();
        }
    }
}