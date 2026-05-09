using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Account;

internal sealed class GetOwnFollows(
    FictionDbContext context,
    UserService userService) : IUseCase, INameEndpoint<GetOwnFollows>
{
    internal sealed record GetOwnFollowsQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetOwnFollowsResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record OwnFollowedAuthorItem(Ulid AuthorId, string Name);
    internal sealed record OwnFollowedStoryItem(Ulid StoryId, string Title, bool IsPublished);
    internal sealed record GetOwnFollowsResponse(
        IEnumerable<OwnFollowedAuthorItem> Authors,
        IEnumerable<OwnFollowedStoryItem> Stories);

    public async Task<Result<GetOwnFollowsResponse>> HandleAsync(
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var userResult = await userService.GetUserAsync(claimsPrincipal, cancellationToken);
        if (userResult.IsFailure) return userResult.DomainError;

        var user = userResult.Value;

        var authors = await context.UserAuthorFollows
            .AsNoTracking()
            .Where(follow => follow.UserId == user.Id)
            .Join(
                context.Authors.AsNoTracking(),
                follow => follow.AuthorId,
                author => author.Id,
                (follow, author) => new { author.Id, author.Name })
            .OrderBy(item => item.Name)
            .Select(item => new OwnFollowedAuthorItem(item.Id, item.Name))
            .ToListAsync(cancellationToken);

        var stories = await context.UserStoryFollows
            .AsNoTracking()
            .Where(follow => follow.UserId == user.Id)
            .Join(
                context.Stories.AsNoTracking(),
                follow => follow.StoryId,
                story => story.Id,
                (follow, story) => new { story.Id, story.Title, IsPublished = story.PublishedAt != null })
            .OrderBy(item => item.Title)
            .Select(item => new OwnFollowedStoryItem(item.Id, item.Title, item.IsPublished))
            .ToListAsync(cancellationToken);

        return new GetOwnFollowsResponse(authors, stories);
    }

    public static string EndpointName => nameof(GetOwnFollows);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("me/follows", async (
                [AsParameters] GetOwnFollowsQuery query,
                GetOwnFollows useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(claimsPrincipal, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Get Followed Authors And Stories")
            .WithDescription("Lists the authors and stories followed by the currently authenticated user.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization()
            .WithStandardResponses(conflict: false, notFound: false)
            .Produces<GetOwnFollowsResponse>();
        }
    }
}