using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Notifications.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Notifications;

internal sealed class RegisterDevicePushSubscription(FictionDbContext context) : IUseCase, INameEndpoint<RegisterDevicePushSubscription>
{
    internal sealed record RegisterDevicePushSubscriptionBody(
        [property: Required]
        [property: StringLength(500)]
        string Endpoint,
        [property: Required]
        [property: StringLength(200)]
        string P256dhKey,
        [property: Required]
        [property: StringLength(200)]
        string AuthKey,
        DateTime? ExpiresAt,
        [property: StringLength(500)]
        string? UserAgent);

    internal sealed record RegisterDevicePushSubscriptionResponse(bool IsRegistered);

    public async Task<Result<RegisterDevicePushSubscriptionResponse>> HandleAsync(
        RegisterDevicePushSubscriptionBody body,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceIdHeader.IsValid(deviceId)) return CommonErrors.Device.InvalidIdentifier;
        if (!IsValid(body)) return RegisterOwnPushSubscription.InvalidSubscription;

        var subscription = await context.DevicePushSubscriptions
            .FirstOrDefaultAsync(candidate => candidate.Endpoint == body.Endpoint, cancellationToken);

        if (subscription is null)
        {
            context.DevicePushSubscriptions.Add(new DevicePushSubscription
            {
                DeviceId = deviceId!,
                Endpoint = body.Endpoint,
                P256dhKey = body.P256dhKey,
                AuthKey = body.AuthKey,
                ExpiresAt = body.ExpiresAt,
                UserAgent = body.UserAgent
            });
        }
        else
        {
            subscription.DeviceId = deviceId!;
            subscription.P256dhKey = body.P256dhKey;
            subscription.AuthKey = body.AuthKey;
            subscription.ExpiresAt = body.ExpiresAt;
            subscription.UserAgent = body.UserAgent;
        }

        await context.SaveChangesAsync(cancellationToken);
        return new RegisterDevicePushSubscriptionResponse(true);
    }

    private static bool IsValid(RegisterDevicePushSubscriptionBody body) =>
        !string.IsNullOrWhiteSpace(body.Endpoint)
        && !string.IsNullOrWhiteSpace(body.P256dhKey)
        && !string.IsNullOrWhiteSpace(body.AuthKey);

    public static string EndpointName => nameof(RegisterDevicePushSubscription);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPut("notifications/push-subscription/device", async (
                [FromHeader(Name = DeviceIdHeader.Name)] string? deviceId,
                [FromBody] RegisterDevicePushSubscriptionBody body,
                RegisterDevicePushSubscription useCase,
                LinkService linker,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(body, deviceId, cancellationToken);
                return result
                    .WithLinks(linker, RegisterDevicePushSubscription.EndpointName, method: HttpMethods.Put)
                    .ToOkResult();
            })
            .WithSummary("Register Device Push Subscription")
            .WithDescription("Registers or refreshes a web push subscription for an anonymous browser or installed PWA device.")
            .WithTags(ApiTags.Authors.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false)
            .Produces<Linked<RegisterDevicePushSubscriptionResponse>>()
            .Accepts<RegisterDevicePushSubscriptionBody>("application/json");
        }
    }
}
