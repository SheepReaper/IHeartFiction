using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Authors;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Validation;

namespace IHFiction.FictionApi.Account;

internal sealed class UpdateOwnAuthorProfile(
    FictionDbContext context,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<UpdateOwnAuthorProfile>
{
    internal static class Errors
    {
        public static readonly DomainError UnsupportedSocialLinkType =
            new("UpdateOwnAuthorProfile.UnsupportedSocialLinkType", "Only supported social link types can be saved.");

        public static readonly DomainError InvalidSocialLinkValue =
            new("UpdateOwnAuthorProfile.InvalidSocialLinkValue", "One or more social links are invalid.");
    }

    /// <summary>
    /// Request body model for updating an author's profile.
    /// </summary>
    /// <param name="Bio">Author's biography or description. Markdown supported.</param>
    /// <param name="SocialLinks">Typed social links for the author profile.</param>
    internal sealed record UpdateOwnAuthorProfileBody(
        [property: StringLength(2000, MinimumLength = 10, ErrorMessage = "Bio cannot exceed 2000 characters.")]
        [property: NoExcessiveWhitespace(5)]
        [property: NoHarmfulContent]
        string? Bio = null,
        [property: MaxLength(20, ErrorMessage = "No more than 20 social links can be submitted.")]
        ICollection<UpdateOwnAuthorSocialLink>? SocialLinks = null
    );

    internal sealed record UpdateOwnAuthorSocialLink(
        [property: Required]
        [property: StringLength(50)]
        string Type,
        [property: Required]
        [property: StringLength(500)]
        string Value);

    internal sealed record UpdateOwnAuthorProfileQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UpdateOwnAuthorProfileResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    /// <summary>
    /// Represents an author's profile information.
    /// </summary>
    /// <param name="Bio">Author's biography or description</param>
    /// <param name="SocialLinks">Author social links keyed by type</param>
    internal sealed record UoapAuthorProfile(string? Bio, IEnumerable<UoapAuthorSocialLink> SocialLinks);

    internal sealed record UoapAuthorSocialLink(string Type, string Value);

    /// <summary>
    /// Response model for updating an author's profile.
    /// </summary>
    /// <param name="Id">Unique identifier for the author</param>
    /// <param name="UserId">The user ID associated with this author</param>
    /// <param name="Name">Display name of the author</param>
    /// <param name="GravatarEmail">Email address for Gravatar profile picture</param>
    /// <param name="UpdatedAt">When the author profile was last updated</param>
    /// <param name="Profile">Updated profile information including bio</param>
    internal sealed record UpdateOwnAuthorProfileResponse(
        Ulid Id,
        Guid UserId,
        string Name,
        string? GravatarEmail,
        DateTime UpdatedAt,
        UoapAuthorProfile Profile);

    public async Task<Result<UpdateOwnAuthorProfileResponse>> HandleAsync(
        UpdateOwnAuthorProfileBody body,
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

        var incomingSocialLinks = body.SocialLinks ?? [];
        var sanitizedLinksByType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var socialLink in incomingSocialLinks)
        {
            if (!AuthorSocialLinkTypes.IsSupported(socialLink.Type))
            {
                return Errors.UnsupportedSocialLinkType;
            }

            var canonicalValue = InputSanitizationService.SanitizeSocialLinkValue(socialLink.Type, socialLink.Value);

            if (string.IsNullOrWhiteSpace(canonicalValue))
            {
                return Errors.InvalidSocialLinkValue;
            }

            sanitizedLinksByType[socialLink.Type.Trim()] = canonicalValue;
        }

        author.Profile.SocialLinks.Clear();
        foreach (var socialLink in sanitizedLinksByType.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            author.Profile.SocialLinks.Add(new()
            {
                Type = socialLink.Key,
                Value = socialLink.Value
            });
        }

        // Save changes - exceptions will be handled by global exception handling
        await context.SaveChangesAsync(cancellationToken);

        return new UpdateOwnAuthorProfileResponse(
            author.Id,
            author.UserId,
            author.Name,
            author.GravatarEmail,
            author.UpdatedAt,
            new UoapAuthorProfile(
                author.Profile.Bio,
                author.Profile.SocialLinks
                    .OrderBy(link => link.Type)
                    .Select(link => new UoapAuthorSocialLink(link.Type, link.Value))));
    }
    public static string EndpointName => nameof(UpdateOwnAuthorProfile);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPut("me/author", async (
                [AsParameters] UpdateOwnAuthorProfileQuery query,
                [FromBody] UpdateOwnAuthorProfileBody body,
                UpdateOwnAuthorProfile useCase,
                LinkService linker,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(body, claimsPrincipal, cancellationToken);

                return result
                    .WithLinks(linker, GetOwnAuthorProfile.EndpointName)
                    .ToOkResult(query);
            })
            .WithSummary("Update Author Profile")
            .WithDescription("Updates the profile information for the currently authenticated author. " +
                "Allows modification of the author's biography and other profile details. " +
                "The biography supports markdown formatting and is subject to content validation. " +
                "This endpoint requires authentication and author registration.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization("author") // Requires authentication
            .WithStandardResponses(conflict: false, notFound: false)
            .Produces<Linked<UpdateOwnAuthorProfileResponse>>()
            .Accepts<UpdateOwnAuthorProfileBody>(MediaTypeNames.Application.Json);
        }
    }
}
