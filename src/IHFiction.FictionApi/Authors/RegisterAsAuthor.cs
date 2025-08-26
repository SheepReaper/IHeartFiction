using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;

using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;

using Error = IHFiction.SharedKernel.Infrastructure.DomainError;
using IHFiction.SharedKernel.Linking;
using System.Net.Mime;

namespace IHFiction.FictionApi.Authors;

internal sealed class RegisterAsAuthor(
    KeycloakAdminService keycloakAdminService,
    UserService userService) : IUseCase, INameEndpoint<RegisterAsAuthor>
{
    const string? ClientId = "fiction-api";

    internal static class Errors
    {
        public static readonly Error User = new("RegisterAuthor.GetOrCreateUser", "Failed to get or create user.");
        public static readonly Error SetAuthorRole = new("RegisterAuthor.KeyCloakRole", "Failed to set author role for user.");
    }

    /// <summary>
    /// Request model for registering as an author.
    /// </summary>
    /// <param name="AcceptTerms">Whether the user has accepted the terms and conditions</param>
    internal sealed record RegisterAsAuthorBody(
        [property: AllowedValues([true], ErrorMessage = "You must accept the terms and conditions to become an author.")]
        bool AcceptTerms
    );

    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<RegisterAsAuthorResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for registering as an author.
    /// </summary>
    /// <param name="Id">Unique identifier for the author</param>
    /// <param name="UserId">The user ID associated with this author</param>
    /// <param name="Name">Display name of the author</param>
    /// <param name="GravatarEmail">Email address for Gravatar profile picture</param>
    /// <param name="UpdatedAt">When the author profile was last updated</param>
    /// <param name="Profile">Author's profile information including bio</param>
    internal sealed record RegisterAsAuthorResponse(
        Ulid Id,
        Guid UserId,
        string Name,
        string? GravatarEmail,
        DateTime UpdatedAt,
        AuthorProfile Profile);

    /// <summary>
    /// Represents an author's profile information.
    /// </summary>
    /// <param name="Bio">Author's biography or description</param>
    internal sealed record AuthorProfile(string? Bio);

    public async Task<Result<RegisterAsAuthorResponse>> HandleAsync(
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var userResult = await userService.GetOrCreateUserAsync(claimsPrincipal, cancellationToken);

        if (userResult.IsFailure) return userResult.DomainError;

        var user = userResult.Value;

        var roleAssignResult = await keycloakAdminService.AssignRoleToUserAsync(user.UserId, "author", ClientId, cancellationToken);

        if (roleAssignResult.IsFailure) return roleAssignResult.DomainError;

        var authorResult = await userService.PromoteToAuthorAsync(user, cancellationToken);

        if (authorResult.IsFailure) return authorResult.DomainError;

        var author = authorResult.Value;

        return new RegisterAsAuthorResponse(
            author.Id,
            author.UserId,
            author.Name,
            author.GravatarEmail,
            author.UpdatedAt,
            new AuthorProfile(author.Profile.Bio));
    }
        public static string EndpointName => nameof(RegisterAsAuthor);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("me/author", async (
                [AsParameters] Query query,
                [FromBody] RegisterAsAuthorBody body,
                RegisterAsAuthor useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(claimsPrincipal, cancellationToken);

                return result.ToCreatedResult($"/authors/{result.Value?.Id}", query);
            })
            .WithSummary("Register as Author")
            .WithDescription("Registers the currently authenticated user as an author, enabling them to create and manage stories. " +
                "This process creates an author profile, assigns the author role in the identity system, " +
                "and sets up the necessary permissions for content creation. " +
                "This endpoint requires authentication but not existing author status.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization() // Requires authentication
            .WithStandardResponses(conflict: false, forbidden: false, notFound: false)
            .Accepts<RegisterAsAuthorBody>(MediaTypeNames.Application.Json)
            .Produces<Linked<RegisterAsAuthorResponse>>(StatusCodes.Status201Created);
        }
    }
}
