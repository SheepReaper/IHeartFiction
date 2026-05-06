using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

using Error = IHFiction.SharedKernel.Infrastructure.DomainError;

namespace IHFiction.FictionApi.Stories;

internal sealed class DeleteStoryCover(
    FictionDbContext context,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<DeleteStoryCover>
{
    internal static class Errors
    {
        public static readonly Error DatabaseError = CommonErrors.Database.SaveFailed;
        public static readonly Error ConcurrencyConflict = CommonErrors.Database.ConcurrencyConflict;
    }

    internal sealed record DeleteStoryCoverResponse(
        Ulid StoryId,
        bool HasCoverImage,
        DateTime UpdatedAt);

    public async Task<Result<DeleteStoryCoverResponse>> HandleAsync(
        Ulid id,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var authResult = await authorizationService.AuthorizeStoryAccessAsync(
            id,
            claimsPrincipal,
            StoryAccessLevel.Edit,
            cancellationToken: cancellationToken);

        if (authResult.IsFailure)
        {
            return authResult.DomainError;
        }

        var (story, _, _) = authResult.Value;

        var existingCover = await context.StoryCovers
            .SingleOrDefaultAsync(cover => cover.StoryId == story.Id, cancellationToken);

        if (existingCover is null)
        {
            return new DeleteStoryCoverResponse(story.Id, false, story.UpdatedAt);
        }

        context.StoryCovers.Remove(existingCover);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Errors.ConcurrencyConflict;
        }
        catch (DbUpdateException)
        {
            return Errors.DatabaseError;
        }

        return new DeleteStoryCoverResponse(story.Id, false, DateTime.UtcNow);
    }

    public static string EndpointName => nameof(DeleteStoryCover);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapDelete("stories/{id:ulid}/cover", async (
                [FromRoute] Ulid id,
                DeleteStoryCover useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);

                return result.ToOkResult();
            })
            .WithSummary("Delete Story Cover")
            .WithDescription("Removes the cover image for a story. Only the story owner or authorized collaborators can delete the cover image. Requires authentication.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses(validation: false)
            .Produces<Linked<DeleteStoryCoverResponse>>();
        }
    }
}