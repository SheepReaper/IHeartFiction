using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Stories;

internal sealed class GetStoryCover(
    FictionDbContext context,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<GetStoryCover>
{
    internal sealed record StoryCoverFile(byte[] Content, string ContentType, bool IsPublic);

    public async Task<Result<StoryCoverFile>> HandleAsync(
        Ulid id,
        System.Security.Claims.ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var story = await context.Stories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(s => new
            {
                s.Id,
                s.PublishedAt
            })
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (story is null)
        {
            return CommonErrors.Story.NotFound;
        }

        var cover = await context.StoryCovers
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.StoryId == id, cancellationToken);

        if (cover is null)
        {
            return CommonErrors.Story.NotFound;
        }

        if (story.PublishedAt.HasValue)
        {
            return new StoryCoverFile(cover.Content, cover.ContentType, true);
        }

        if (claimsPrincipal.Identity?.IsAuthenticated != true)
        {
            return CommonErrors.Story.NotFound;
        }

        var authResult = await authorizationService.AuthorizeStoryAccessAsync(
            id,
            claimsPrincipal,
            StoryAccessLevel.Read,
            includeDeleted: true,
            cancellationToken: cancellationToken);

        if (authResult.IsFailure)
        {
            return authResult.DomainError;
        }

        return new StoryCoverFile(cover.Content, cover.ContentType, false);
    }

    public static string EndpointName => nameof(GetStoryCover);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("stories/{id:ulid}/cover", async (
                [FromRoute] Ulid id,
                HttpContext httpContext,
                GetStoryCover useCase,
                System.Security.Claims.ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);

                if (result.IsFailure)
                {
                    return result.DomainError.ToProblemDetailsResult();
                }

                httpContext.Response.Headers.CacheControl = result.Value.IsPublic
                    ? "public,max-age=3600"
                    : "private,no-store";

                return Results.File(result.Value.Content, result.Value.ContentType);
            })
            .WithSummary("Get Story Cover")
            .WithDescription("Retrieves the cover image for a story. Published story covers are public. Draft covers require story read access.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, unauthorized: false, validation: false)
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        }
    }
}