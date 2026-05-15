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

internal sealed class MarkDeviceNotificationRead(
    FictionDbContext context,
    TimeProvider timeProvider) : IUseCase, INameEndpoint<MarkDeviceNotificationRead>
{
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
        if (!DeviceIdHeader.IsValid(deviceId)) return CommonErrors.Device.InvalidIdentifier;

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