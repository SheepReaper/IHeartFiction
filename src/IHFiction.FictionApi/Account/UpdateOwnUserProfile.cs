using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Validation;

namespace IHFiction.FictionApi.Account;

internal sealed class UpdateOwnUserProfile(
    FictionDbContext context,
    UserService userService) : IUseCase, INameEndpoint<UpdateOwnUserProfile>
{
    /// <summary>
    /// Request body model for updating the current user's profile.
    /// </summary>
    /// <param name="Name">Display name of the user</param>
    /// <param name="GravatarEmail">Email address for Gravatar profile picture</param>
    internal sealed record UpdateOwnUserProfileBody(
        [property: Required(ErrorMessage = "Name is required.")]
        [property: StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters.")]
        [property: NoExcessiveWhitespace(2)]
        [property: NoHarmfulContent]
        string Name,

        [property: EmailAddress(ErrorMessage = "Gravatar email must be a valid email address.")]
        [property: StringLength(256, ErrorMessage = "Gravatar email cannot exceed 256 characters.")]
        [property: NoHarmfulContent]
        string? GravatarEmail = null
    );

    internal sealed record UpdateOwnUserProfileQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UpdateOwnUserProfileResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for updating the current user's profile.
    /// </summary>
    /// <param name="Name">Updated display name of the user</param>
    /// <param name="GravatarEmail">Updated email address for Gravatar profile picture</param>
    internal sealed record UpdateOwnUserProfileResponse(
        string Name,
        string? GravatarEmail);

    public async Task<Result<UpdateOwnUserProfileResponse>> HandleAsync(
        UpdateOwnUserProfileBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var userResult = await userService.GetUserAsync(claimsPrincipal, cancellationToken);

        if (userResult.IsFailure) return userResult.DomainError;

        var user = userResult.Value;

        user.Name = InputSanitizationService.SanitizeText(body.Name);
        user.GravatarEmail = InputSanitizationService.SanitizeOptionalText(body.GravatarEmail);

        await context.SaveChangesAsync(cancellationToken);

        return new UpdateOwnUserProfileResponse(user.Name, user.GravatarEmail);
    }

    public static string EndpointName => nameof(UpdateOwnUserProfile);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPut("me/profile", async (
                [AsParameters] UpdateOwnUserProfileQuery query,
                [FromBody] UpdateOwnUserProfileBody body,
                UpdateOwnUserProfile useCase,
                LinkService linker,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(body, claimsPrincipal, cancellationToken);

                return result
                    .WithLinks(linker, GetOwnUserProfile.EndpointName)
                    .ToOkResult(query);
            })
            .WithSummary("Update User Profile")
            .WithDescription("Updates the display name and Gravatar email for the currently authenticated user. " +
                "Name is required; Gravatar email is optional.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization()
            .WithStandardResponses(conflict: false, notFound: false)
            .Produces<Linked<UpdateOwnUserProfileResponse>>()
            .Accepts<UpdateOwnUserProfileBody>(MediaTypeNames.Application.Json);
        }
    }
}
