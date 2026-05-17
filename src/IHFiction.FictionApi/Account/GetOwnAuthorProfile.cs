using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Account;

internal sealed class GetOwnAuthorProfile(
    AuthorizationService authorizationService,
    FictionDbContext context) : IUseCase, INameEndpoint<GetOwnAuthorProfile>
{
    internal sealed record GetOwnAuthorProfileQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetOwnAuthorProfileResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    /// <summary>
    /// Represents the current authenticated author's profile information.
    /// </summary>
    /// <param name="Bio">Author's biography or description</param>
    /// <param name="SocialLinks">Author social links keyed by type</param>
    internal sealed record OwnAuthorProfile(string? Bio, IEnumerable<OwnAuthorSocialLink> SocialLinks);

    internal sealed record OwnAuthorSocialLink(string Type, string Value);

    /// <summary>
    /// Represents a work (story) associated with the current author.
    /// </summary>
    /// <param name="Id">Unique identifier for the work</param>
    /// <param name="Title">Title of the work</param>
    internal sealed record OwnAuthorWorkItem(Ulid Id, string Title);

    internal sealed record GetOwnAuthorProfileResponse(
        Ulid Id,
        Guid UserId,
        string Name,
        string? GravatarEmail,
        DateTime UpdatedAt,
        OwnAuthorProfile Profile,
        IEnumerable<OwnAuthorWorkItem> Works,
        IEnumerable<OwnAuthorWorkItem> OwnedWorks);

    public async Task<Result<GetOwnAuthorProfileResponse>> HandleAsync(
        ClaimsPrincipal claimsPrincipal, CancellationToken cancellationToken = default)
    {
        var authorResult = await authorizationService.GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);

        if (authorResult.IsFailure) return authorResult.DomainError;

        var entry = context.Entry(authorResult.Value);

        var author = entry.Entity;

        var works = entry.Collection(a => a.Works).Query().Where(w => w is Story);

        var ownedWorks = works.Where(w => w.OwnerId == author.Id);

        return new GetOwnAuthorProfileResponse(
            author.Id,
            author.UserId,
            author.Name,
            author.GravatarEmail,
            author.UpdatedAt,
            new OwnAuthorProfile(
                author.Profile.Bio,
                author.Profile.SocialLinks
                    .OrderBy(link => link.Type)
                    .Select(link => new OwnAuthorSocialLink(link.Type, link.Value))),
            works.Select(work => new OwnAuthorWorkItem(work.Id, work.Title)),
            ownedWorks.Select(work => new OwnAuthorWorkItem(work.Id, work.Title)));
    }

    public static string EndpointName => nameof(GetOwnAuthorProfile);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("me/author", async (
                [AsParameters] GetOwnAuthorProfileQuery query,
                GetOwnAuthorProfile useCase,
                LinkService linker,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(claimsPrincipal, cancellationToken);

                return result
                    .WithLinks(linker, GetOwnAuthorProfile.EndpointName)
                    .ToOkResult(query);
            })
            .WithSummary("Get Own Author Profile")
            .WithDescription("Retrieves the profile and works for the currently authenticated author. This endpoint requires authentication and author registration.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization("author")
            .WithStandardResponses(conflict: false, notFound: false)
            .Produces<Linked<GetOwnAuthorProfileResponse>>();
        }
    }
}