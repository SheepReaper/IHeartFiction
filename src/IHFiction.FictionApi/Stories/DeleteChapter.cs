using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Infrastructure;

using MongoDB.Driver;

namespace IHFiction.FictionApi.Stories;

internal sealed class DeleteChapter(
    FictionDbContext context,
    IMongoCollection<WorkBody> workBodies,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<DeleteChapter>
{
    internal static class Errors
    {
        public static readonly DomainError ChapterNotFound = new("DeleteChapter.NotFound", "The specified chapter does not exist.");
        public static readonly DomainError DatabaseError = CommonErrors.Database.SaveFailed;
        public static readonly DomainError AlreadyDeleted = new("DeleteChapter.AlreadyDeleted", "This chapter has already been deleted.");
    }

    public async Task<Result> HandleAsync(Ulid id, ClaimsPrincipal claimsPrincipal, CancellationToken cancellationToken = default)
    {
        var authResult = await authorizationService.AuthorizeChapterAccessAsync(id, claimsPrincipal, StoryAccessLevel.Delete, true, cancellationToken);
        if (authResult.IsFailure) return authResult.DomainError;

        var chapter = authResult.Value.Chapter;

        if (chapter.DeletedAt.HasValue)
        {
            return Errors.AlreadyDeleted;
        }

        var workBodyId = chapter.WorkBodyId;

        try
        {
            context.Chapters.Remove(chapter);

            // TODO: Implement distributed transaction

            await workBodies.FindOneAndDeleteAsync(wb => wb.Id == workBodyId, cancellationToken: cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateException)
        {
            return Errors.DatabaseError;
        }
    }
        public static string EndpointName => nameof(DeleteChapter);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapDelete("chapters/{id:ulid}", async (
                [FromRoute] Ulid id,
                DeleteChapter useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);

                return result.ToDeletedResult();
            })
            .WithSummary("Delete Chapter")
            .WithDescription("Permanently deletes a chapter and its associated content.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses(conflict: false, validation: false)
            .Produces(StatusCodes.Status204NoContent);
        }
    }
}
