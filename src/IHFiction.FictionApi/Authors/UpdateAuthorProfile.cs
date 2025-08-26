using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Validation;
using IHFiction.SharedKernel.Linking;
using System.Net.Mime;

namespace IHFiction.FictionApi.Authors;

internal sealed class UpdateAuthorProfile(
    FictionDbContext context,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<UpdateAuthorProfile>
{
    /// <summary>
    /// Request body model for updating an author's profile.
    /// </summary>
    /// <param name="Bio">Author's biography or description. Markdown supported.</param>
    internal sealed record UpdateAuthorProfileBody(
        [property: StringLength(2000, MinimumLength = 10, ErrorMessage = "Bio cannot exceed 2000 characters.")]
        [property: NoExcessiveWhitespace(5)]
        [property: NoHarmfulContent]
        string? Bio = null
    );

    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UpdateAuthorProfileResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Represents an author's profile information.
    /// </summary>
    /// <param name="Bio">Author's biography or description</param>
    internal sealed record AuthorProfile(string? Bio);

    /// <summary>
    /// Response model for updating an author's profile.
    /// </summary>
    /// <param name="Id">Unique identifier for the author</param>
    /// <param name="UserId">The user ID associated with this author</param>
    /// <param name="Name">Display name of the author</param>
    /// <param name="GravatarEmail">Email address for Gravatar profile picture</param>
    /// <param name="UpdatedAt">When the author profile was last updated</param>
    /// <param name="Profile">Updated profile information including bio</param>
    internal sealed record UpdateAuthorProfileResponse(
        Ulid Id,
        Guid UserId,
        string Name,
        string? GravatarEmail,
        DateTime UpdatedAt,
        AuthorProfile Profile);

    public async Task<Result<UpdateAuthorProfileResponse>> HandleAsync(
        UpdateAuthorProfileBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        // Get the current author using the centralized authorization service
        var authorResult = await authorizationService.GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);
        if (authorResult.IsFailure) return authorResult.DomainError;

        var author = authorResult.Value;

        // Sanitize and update the profile using the centralized service
        var sanitizedBio = InputSanitizationService.SanitizeBio(body.Bio);
        author.Profile.Bio = sanitizedBio;

        // Save changes - exceptions will be handled by global exception handling
        await context.SaveChangesAsync(cancellationToken);

        return new UpdateAuthorProfileResponse(
            author.Id,
            author.UserId,
            author.Name,
            author.GravatarEmail,
            author.UpdatedAt,
            new AuthorProfile(author.Profile.Bio));
    }
        public static string EndpointName => nameof(UpdateAuthorProfile);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;


        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPut("me/author", async (
                [AsParameters] Query query,
                [FromBody] UpdateAuthorProfileBody body,
                UpdateAuthorProfile useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(body, claimsPrincipal, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Update Author Profile")
            .WithDescription("Updates the profile information for the currently authenticated author. " +
                "Allows modification of the author's biography and other profile details. " +
                "The biography supports markdown formatting and is subject to content validation. " +
                "This endpoint requires authentication and author registration.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization("author") // Requires authentication
            .WithStandardResponses(conflict: false, notFound: false)
            .Produces<Linked<UpdateAuthorProfileResponse>>()
            .Accepts<UpdateAuthorProfileBody>(MediaTypeNames.Application.Json);
        }
    }
}
