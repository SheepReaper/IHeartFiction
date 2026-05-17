using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Account;

internal sealed class GetOwnUserProfile(
    UserService userService,
    FictionDbContext context) : IUseCase, INameEndpoint<GetOwnUserProfile>
{
    internal sealed record GetOwnUserProfileQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetOwnUserProfileResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for getting the current authenticated user's profile.
    /// </summary>
    /// <param name="Name">Display name of the user</param>
    /// <param name="GravatarEmail">Email address for Gravatar profile picture</param>
    /// <param name="IsAuthor">Whether the user is registered as an author</param>
    internal sealed record GetOwnUserProfileResponse(
        string Name,
        string? GravatarEmail,
        bool IsAuthor);

    public async Task<Result<GetOwnUserProfileResponse>> HandleAsync(
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var userResult = await userService.GetUserAsync(claimsPrincipal, cancellationToken);

        if (userResult.IsFailure) return userResult.DomainError;

        var user = userResult.Value;

        var isAuthor = await context.Authors.AnyAsync(a => a.UserId == user.UserId, cancellationToken);

        return new GetOwnUserProfileResponse(user.Name, user.GravatarEmail, isAuthor);
    }

    public static string EndpointName => nameof(GetOwnUserProfile);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("me/profile", async (
                [AsParameters] GetOwnUserProfileQuery query,
                GetOwnUserProfile useCase,
                LinkService linker,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(claimsPrincipal, cancellationToken);

                return result
                    .WithLinks(linker, GetOwnUserProfile.EndpointName)
                    .ToOkResult(query);
            })
            .WithSummary("Get User Profile")
            .WithDescription("Retrieves the display name and Gravatar email for the currently authenticated user, " +
                "along with a flag indicating whether the user is registered as an author.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization()
            .WithStandardResponses(conflict: false, notFound: false)
            .Produces<Linked<GetOwnUserProfileResponse>>();
        }
    }
}
