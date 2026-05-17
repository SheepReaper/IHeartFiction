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

namespace IHFiction.FictionApi.Authors;

internal sealed class FollowAuthor(
    FictionDbContext context,
    UserService userService) : IUseCase, INameEndpoint<FollowAuthor>
{
    internal sealed record FollowAuthorQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<FollowAuthorResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record FollowAuthorResponse(Ulid AuthorId, bool IsFollowing);

    public async Task<Result<FollowAuthorResponse>> HandleAsync(
        Ulid id,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var userResult = await userService.GetUserAsync(claimsPrincipal, cancellationToken);
        if (userResult.IsFailure) return userResult.DomainError;

        var exists = await context.Authors.AnyAsync(author => author.Id == id, cancellationToken);
        if (!exists) return CommonErrors.Author.NotFound;

        var user = userResult.Value;
        var existing = await context.UserAuthorFollows
            .FirstOrDefaultAsync(follow => follow.UserId == user.Id && follow.AuthorId == id, cancellationToken);

        if (existing is null)
        {
            context.UserAuthorFollows.Add(new UserAuthorFollow
            {
                UserId = user.Id,
                AuthorId = id
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        return new FollowAuthorResponse(id, true);
    }

    public static string EndpointName => nameof(FollowAuthor);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("authors/{id:ulid}/follow", async (
                [FromRoute] Ulid id,
                [AsParameters] FollowAuthorQuery query,
                FollowAuthor useCase,
                LinkService linker,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);
                return result
                    .WithLinks(linker, GetAuthor.EndpointName, values: [new KeyValuePair<string, string?>("id", id.ToString())])
                    .ToOkResult(query);
            })
            .WithSummary("Follow Author")
            .WithDescription("Follows an author for the currently authenticated user. This operation is idempotent.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization()
            .WithStandardResponses(conflict: false)
            .Produces<Linked<FollowAuthorResponse>>();
        }
    }
}

internal sealed class UnfollowAuthor(
    FictionDbContext context,
    UserService userService) : IUseCase, INameEndpoint<UnfollowAuthor>
{
    internal sealed record UnfollowAuthorQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UnfollowAuthorResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record UnfollowAuthorResponse(Ulid AuthorId, bool IsFollowing);

    public async Task<Result<UnfollowAuthorResponse>> HandleAsync(
        Ulid id,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var userResult = await userService.GetUserAsync(claimsPrincipal, cancellationToken);
        if (userResult.IsFailure) return userResult.DomainError;

        var exists = await context.Authors.AnyAsync(author => author.Id == id, cancellationToken);
        if (!exists) return CommonErrors.Author.NotFound;

        var user = userResult.Value;
        var existing = await context.UserAuthorFollows
            .FirstOrDefaultAsync(follow => follow.UserId == user.Id && follow.AuthorId == id, cancellationToken);

        if (existing is not null)
        {
            context.UserAuthorFollows.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
        }

        return new UnfollowAuthorResponse(id, false);
    }

    public static string EndpointName => nameof(UnfollowAuthor);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapDelete("authors/{id:ulid}/follow", async (
                [FromRoute] Ulid id,
                [AsParameters] UnfollowAuthorQuery query,
                UnfollowAuthor useCase,
                LinkService linker,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);
                return result
                    .WithLinks(linker, GetAuthor.EndpointName, values: [new KeyValuePair<string, string?>("id", id.ToString())])
                    .ToOkResult(query);
            })
            .WithSummary("Unfollow Author")
            .WithDescription("Unfollows an author for the currently authenticated user. This operation is idempotent.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization()
            .WithStandardResponses(conflict: false)
            .Produces<Linked<UnfollowAuthorResponse>>();
        }
    }
}

internal sealed class FollowAuthorForDevice(FictionDbContext context) : IUseCase, INameEndpoint<FollowAuthorForDevice>
{
    internal sealed record FollowAuthorForDeviceQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<FollowAuthorForDeviceResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record FollowAuthorForDeviceResponse(Ulid AuthorId, bool IsFollowing);

    public async Task<Result<FollowAuthorForDeviceResponse>> HandleAsync(
        Ulid id,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceIdHeader.IsValid(deviceId)) return CommonErrors.Device.InvalidIdentifier;

        var exists = await context.Authors.AnyAsync(author => author.Id == id, cancellationToken);
        if (!exists) return CommonErrors.Author.NotFound;

        var existing = await context.DeviceAuthorFollows
            .FirstOrDefaultAsync(follow => follow.DeviceId == deviceId && follow.AuthorId == id, cancellationToken);

        if (existing is null)
        {
            context.DeviceAuthorFollows.Add(new DeviceAuthorFollow
            {
                DeviceId = deviceId!,
                AuthorId = id
            });

            await context.SaveChangesAsync(cancellationToken);
        }

        return new FollowAuthorForDeviceResponse(id, true);
    }

    public static string EndpointName => nameof(FollowAuthorForDevice);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("authors/{id:ulid}/follow/device", async (
                [FromRoute] Ulid id,
                [FromHeader(Name = DeviceIdHeader.Name)] string? deviceId,
                [AsParameters] FollowAuthorForDeviceQuery query,
                FollowAuthorForDevice useCase,
                LinkService linker,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, deviceId, cancellationToken);
                return result
                    .WithLinks(linker, GetAuthor.EndpointName, values: [new KeyValuePair<string, string?>("id", id.ToString())])
                    .ToOkResult(query);
            })
            .WithSummary("Follow Author For Device")
            .WithDescription("Follows an author for an anonymous browser or installed PWA device. Requires a valid device identifier header.")
            .WithTags(ApiTags.Authors.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false)
            .Produces<Linked<FollowAuthorForDeviceResponse>>();
        }
    }
}

internal sealed class UnfollowAuthorForDevice(FictionDbContext context) : IUseCase, INameEndpoint<UnfollowAuthorForDevice>
{
    internal sealed record UnfollowAuthorForDeviceQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UnfollowAuthorForDeviceResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record UnfollowAuthorForDeviceResponse(Ulid AuthorId, bool IsFollowing);

    public async Task<Result<UnfollowAuthorForDeviceResponse>> HandleAsync(
        Ulid id,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!DeviceIdHeader.IsValid(deviceId)) return CommonErrors.Device.InvalidIdentifier;

        var exists = await context.Authors.AnyAsync(author => author.Id == id, cancellationToken);
        if (!exists) return CommonErrors.Author.NotFound;

        var existing = await context.DeviceAuthorFollows
            .FirstOrDefaultAsync(follow => follow.DeviceId == deviceId && follow.AuthorId == id, cancellationToken);

        if (existing is not null)
        {
            context.DeviceAuthorFollows.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
        }

        await DevicePushSubscriptionCleanup.RemoveIfDeviceHasNoFollowsAsync(context, deviceId!, cancellationToken);

        return new UnfollowAuthorForDeviceResponse(id, false);
    }

    public static string EndpointName => nameof(UnfollowAuthorForDevice);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapDelete("authors/{id:ulid}/follow/device", async (
                [FromRoute] Ulid id,
                [FromHeader(Name = DeviceIdHeader.Name)] string? deviceId,
                [AsParameters] UnfollowAuthorForDeviceQuery query,
                UnfollowAuthorForDevice useCase,
                LinkService linker,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, deviceId, cancellationToken);
                return result
                    .WithLinks(linker, GetAuthor.EndpointName, values: [new KeyValuePair<string, string?>("id", id.ToString())])
                    .ToOkResult(query);
            })
            .WithSummary("Unfollow Author For Device")
            .WithDescription("Unfollows an author for an anonymous browser or installed PWA device. Requires a valid device identifier header.")
            .WithTags(ApiTags.Authors.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false)
            .Produces<Linked<UnfollowAuthorForDeviceResponse>>();
        }
    }
}
