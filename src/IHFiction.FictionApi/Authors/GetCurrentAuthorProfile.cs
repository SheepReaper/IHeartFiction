using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Authors;

internal sealed class GetCurrentAuthorProfile(
    AuthorizationService authorizationService,
    FictionDbContext context) : IUseCase, INameEndpoint<GetCurrentAuthorProfile>
{
    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetCurrentAuthorProfileResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Represents the current authenticated author's profile information.
    /// </summary>
    /// <param name="Bio">Author's biography or description</param>
    internal sealed record CurrentAuthorProfile(string? Bio);

    /// <summary>
    /// Represents a work (story) associated with the current author.
    /// </summary>
    /// <param name="Id">Unique identifier for the work</param>
    /// <param name="Title">Title of the work</param>
    internal sealed record CurrentAuthorWork(Ulid Id, string Title);

    /// <summary>
    /// Response model for getting the current authenticated author's profile and works.
    /// </summary>
    /// <param name="Id">Unique identifier for the author</param>
    /// <param name="UserId">The user ID associated with this author</param>
    /// <param name="Name">Display name of the author</param>
    /// <param name="GravatarEmail">Email address for Gravatar profile picture</param>
    /// <param name="UpdatedAt">When the author profile was last updated</param>
    /// <param name="Profile">Author's profile information including bio</param>
    /// <param name="Works">Collection of works the author collaborates on</param>
    /// <param name="OwnedWorks">Collection of works owned by the author</param>
    internal sealed record GetCurrentAuthorProfileResponse(
        Ulid Id,
        Guid UserId,
        string Name,
        string? GravatarEmail,
        DateTime UpdatedAt,
        CurrentAuthorProfile Profile,
        IEnumerable<CurrentAuthorWork> Works,
        IEnumerable<CurrentAuthorWork> OwnedWorks);

    public async Task<Result<GetCurrentAuthorProfileResponse>> HandleAsync(
        ClaimsPrincipal claimsPrincipal, CancellationToken cancellationToken = default)
    {
        // Get the current author using the centralized authorization service
        var authorResult = await authorizationService.GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);

        if (authorResult.IsFailure) return authorResult.DomainError;

        var entry = context.Entry(authorResult.Value);

        var author = entry.Entity;

        var works = entry.Collection(a => a.Works).Query().Where(w => w is Story);

        var ownedWorks = works.Where(w => w.OwnerId == author.Id);

        return new GetCurrentAuthorProfileResponse(
            author.Id,
            author.UserId,
            author.Name,
            author.GravatarEmail,
            author.UpdatedAt,
            new CurrentAuthorProfile(author.Profile.Bio),
            works.Select(work => new CurrentAuthorWork(work.Id, work.Title)),
            ownedWorks.Select(work => new CurrentAuthorWork(work.Id, work.Title)));
    }
    public static string EndpointName => nameof(GetCurrentAuthorProfile);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("me/author", async (
                [AsParameters] Query query,
                GetCurrentAuthorProfile useCase,
                LinkService linker,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(claimsPrincipal, cancellationToken);

                return result
                    .WithLinks([
                        linker.Create<GetAuthorById>("self", HttpMethods.Get, new{ result.Value!.Id })
                    ]).ToOkResult(query);
            })
            .WithSummary("Get Current Author Profile")
            .WithDescription("Retrieves the profile information and works for the currently authenticated author. " +
                "Returns the author's personal information, biography, and lists of both collaborative " +
                "and owned works. This endpoint requires authentication and author registration.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization("author") // Requires authentication
            .WithStandardResponses(conflict: false)
            .Produces<Linked<GetCurrentAuthorProfileResponse>>();
        }
    }
}
