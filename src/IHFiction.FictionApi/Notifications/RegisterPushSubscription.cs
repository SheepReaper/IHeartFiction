using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

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

internal sealed class RegisterOwnPushSubscription(
    FictionDbContext context,
    UserService userService) : IUseCase, INameEndpoint<RegisterOwnPushSubscription>
{
    internal static readonly DomainError InvalidSubscription =
        new("PushSubscription.Invalid", "A valid push subscription is required.");

    internal sealed record RegisterOwnPushSubscriptionBody(
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

    internal sealed record RegisterOwnPushSubscriptionResponse(bool IsRegistered);

    public async Task<Result<RegisterOwnPushSubscriptionResponse>> HandleAsync(
        RegisterOwnPushSubscriptionBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        if (!IsValid(body)) return InvalidSubscription;

        var userResult = await userService.GetUserAsync(claimsPrincipal, cancellationToken);
        if (userResult.IsFailure) return userResult.DomainError;

        var subscription = await context.UserPushSubscriptions
            .FirstOrDefaultAsync(candidate => candidate.Endpoint == body.Endpoint, cancellationToken);

        if (subscription is null)
        {
            context.UserPushSubscriptions.Add(new UserPushSubscription
            {
                UserId = userResult.Value.Id,
                Endpoint = body.Endpoint,
                P256dhKey = body.P256dhKey,
                AuthKey = body.AuthKey,
                ExpiresAt = body.ExpiresAt,
                UserAgent = body.UserAgent
            });
        }
        else
        {
            subscription.UserId = userResult.Value.Id;
            subscription.P256dhKey = body.P256dhKey;
            subscription.AuthKey = body.AuthKey;
            subscription.ExpiresAt = body.ExpiresAt;
            subscription.UserAgent = body.UserAgent;
        }

        await context.SaveChangesAsync(cancellationToken);
        return new RegisterOwnPushSubscriptionResponse(true);
    }

    private static bool IsValid(RegisterOwnPushSubscriptionBody body) =>
        !string.IsNullOrWhiteSpace(body.Endpoint)
        && !string.IsNullOrWhiteSpace(body.P256dhKey)
        && !string.IsNullOrWhiteSpace(body.AuthKey);

    public static string EndpointName => nameof(RegisterOwnPushSubscription);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPut("notifications/push-subscription", async (
                [FromBody] RegisterOwnPushSubscriptionBody body,
                RegisterOwnPushSubscription useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(body, claimsPrincipal, cancellationToken);
                return result.ToOkResult();
            })
            .WithSummary("Register Push Subscription")
            .WithDescription("Registers or refreshes a web push subscription for the currently authenticated user.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization()
            .WithStandardResponses(conflict: false)
            .Produces<Linked<RegisterOwnPushSubscriptionResponse>>()
            .Accepts<RegisterOwnPushSubscriptionBody>("application/json");
        }
    }
}

internal sealed class RegisterDevicePushSubscription(FictionDbContext context) : IUseCase, INameEndpoint<RegisterDevicePushSubscription>
{
    internal static readonly DomainError InvalidDeviceId =
        new("Device.InvalidIdentifier", "A valid device identifier is required.");

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
        if (!DeviceIdHeader.IsValid(deviceId)) return InvalidDeviceId;
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
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(body, deviceId, cancellationToken);
                return result.ToOkResult();
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
