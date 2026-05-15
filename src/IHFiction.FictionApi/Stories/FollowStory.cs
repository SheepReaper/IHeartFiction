using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Notifications.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.FictionApi.Notifications;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Stories;

internal sealed class FollowStory(
    FictionDbContext context,
    UserService userService) : IUseCase, INameEndpoint<FollowStory>
{
    internal sealed record FollowStoryQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<FollowStoryResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record FollowStoryResponse(Ulid StoryId, bool IsFollowing);

    public async Task<Result<FollowStoryResponse>> HandleAsync(
        Ulid id,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var userResult = await userService.GetUserAsync(claimsPrincipal, cancellationToken);
        if (userResult.IsFailure) return userResult.DomainError;

        var story = await context.Stories
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (story is null) return CommonErrors.Story.NotFound;
        if (!story.IsPublished) return CommonErrors.Story.NotPublished;

        var user = userResult.Value;
        var existing = await context.UserStoryFollows
            .FirstOrDefaultAsync(follow => follow.UserId == user.Id && follow.StoryId == id, cancellationToken);

        if (existing is null)
        {
            context.UserStoryFollows.Add(new UserStoryFollow
            {
                UserId = user.Id,
                StoryId = id
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        return new FollowStoryResponse(id, true);
    }

    public static string EndpointName => nameof(FollowStory);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("stories/{id:ulid}/follow", async (
                [FromRoute] Ulid id,
                [AsParameters] FollowStoryQuery query,
                FollowStory useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Follow Story")
            .WithDescription("Follows a published story for the currently authenticated user. This operation is idempotent.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization()
            .WithStandardResponses(conflict: false)
            .Produces<Linked<FollowStoryResponse>>();
        }
    }
}

internal sealed class UnfollowStory(
    FictionDbContext context,
    UserService userService) : IUseCase, INameEndpoint<UnfollowStory>
{
    internal sealed record UnfollowStoryQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UnfollowStoryResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record UnfollowStoryResponse(Ulid StoryId, bool IsFollowing);

    public async Task<Result<UnfollowStoryResponse>> HandleAsync(
        Ulid id,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var userResult = await userService.GetUserAsync(claimsPrincipal, cancellationToken);
        if (userResult.IsFailure) return userResult.DomainError;

        var story = await context.Stories
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (story is null) return CommonErrors.Story.NotFound;
        if (!story.IsPublished) return CommonErrors.Story.NotPublished;

        var user = userResult.Value;
        var existing = await context.UserStoryFollows
            .FirstOrDefaultAsync(follow => follow.UserId == user.Id && follow.StoryId == id, cancellationToken);

        if (existing is not null)
        {
            context.UserStoryFollows.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
        }

        return new UnfollowStoryResponse(id, false);
    }

    public static string EndpointName => nameof(UnfollowStory);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapDelete("stories/{id:ulid}/follow", async (
                [FromRoute] Ulid id,
                [AsParameters] UnfollowStoryQuery query,
                UnfollowStory useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Unfollow Story")
            .WithDescription("Unfollows a published story for the currently authenticated user. This operation is idempotent.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization()
            .WithStandardResponses(conflict: false)
            .Produces<Linked<UnfollowStoryResponse>>();
        }
    }
}

internal sealed class FollowStoryForDevice(FictionDbContext context) : IUseCase, INameEndpoint<FollowStoryForDevice>
{
    internal static readonly DomainError InvalidDeviceId =
        new("Device.InvalidIdentifier", "A valid device identifier is required.");

    internal sealed record FollowStoryForDeviceQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<FollowStoryForDeviceResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record FollowStoryForDeviceResponse(Ulid StoryId, bool IsFollowing);

    public async Task<Result<FollowStoryForDeviceResponse>> HandleAsync(
        Ulid id,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceIdHeader.IsValid(deviceId)) return InvalidDeviceId;

        var story = await context.Stories
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (story is null) return CommonErrors.Story.NotFound;
        if (!story.IsPublished) return CommonErrors.Story.NotPublished;

        var existing = await context.DeviceStoryFollows
            .FirstOrDefaultAsync(follow => follow.DeviceId == deviceId && follow.StoryId == id, cancellationToken);

        if (existing is null)
        {
            context.DeviceStoryFollows.Add(new DeviceStoryFollow
            {
                DeviceId = deviceId!,
                StoryId = id
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        return new FollowStoryForDeviceResponse(id, true);
    }

    public static string EndpointName => nameof(FollowStoryForDevice);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("stories/{id:ulid}/follow/device", async (
                [FromRoute] Ulid id,
                [FromHeader(Name = DeviceIdHeader.Name)] string? deviceId,
                [AsParameters] FollowStoryForDeviceQuery query,
                FollowStoryForDevice useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, deviceId, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Follow Story For Device")
            .WithDescription("Follows a published story for an anonymous browser or installed PWA device. Requires a valid device identifier header.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false)
            .Produces<Linked<FollowStoryForDeviceResponse>>();
        }
    }
}

internal sealed class UnfollowStoryForDevice(FictionDbContext context) : IUseCase, INameEndpoint<UnfollowStoryForDevice>
{
    internal static readonly DomainError InvalidDeviceId =
        new("Device.InvalidIdentifier", "A valid device identifier is required.");

    internal sealed record UnfollowStoryForDeviceQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UnfollowStoryForDeviceResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record UnfollowStoryForDeviceResponse(Ulid StoryId, bool IsFollowing);

    public async Task<Result<UnfollowStoryForDeviceResponse>> HandleAsync(
        Ulid id,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceIdHeader.IsValid(deviceId)) return InvalidDeviceId;

        var story = await context.Stories
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (story is null) return CommonErrors.Story.NotFound;
        if (!story.IsPublished) return CommonErrors.Story.NotPublished;

        var existing = await context.DeviceStoryFollows
            .FirstOrDefaultAsync(follow => follow.DeviceId == deviceId && follow.StoryId == id, cancellationToken);

        if (existing is not null)
        {
            context.DeviceStoryFollows.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
        }

        await DevicePushSubscriptionCleanup.RemoveIfDeviceHasNoFollowsAsync(context, deviceId!, cancellationToken);

        return new UnfollowStoryForDeviceResponse(id, false);
    }

    public static string EndpointName => nameof(UnfollowStoryForDevice);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapDelete("stories/{id:ulid}/follow/device", async (
                [FromRoute] Ulid id,
                [FromHeader(Name = DeviceIdHeader.Name)] string? deviceId,
                [AsParameters] UnfollowStoryForDeviceQuery query,
                UnfollowStoryForDevice useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, deviceId, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Unfollow Story For Device")
            .WithDescription("Unfollows a published story for an anonymous browser or installed PWA device. Requires a valid device identifier header.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false)
            .Produces<Linked<UnfollowStoryForDeviceResponse>>();
        }
    }
}
