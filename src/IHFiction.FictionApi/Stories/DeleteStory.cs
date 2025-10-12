using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Infrastructure;

using Error = IHFiction.SharedKernel.Infrastructure.DomainError;

namespace IHFiction.FictionApi.Stories;

internal sealed class DeleteStory(
    FictionDbContext context,
    AuthorizationService authorizationService,
    EntityLoaderService entityLoader) : IUseCase, INameEndpoint<DeleteStory>
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly Error StoryNotFound = CommonErrors.Story.NotFound;
        public static readonly Error AuthorNotFound = CommonErrors.Author.NotRegistered;
        public static readonly Error DatabaseError = CommonErrors.Database.SaveFailed;

        // Keep use case-specific errors
        public static readonly Error NotAuthorized = new("DeleteStory.NotAuthorized", "You are not authorized to delete this story. Only the story owner can delete it.");
        public static readonly Error AlreadyDeleted = CommonErrors.Story.AlreadyDeleted;
    }

    public async Task<Result> HandleAsync(
        Ulid id,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        // Get the current author using the centralized authorization service
        var authorResult = await authorizationService.GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);
        if (authorResult.IsFailure) return authorResult.DomainError;

        var author = authorResult.Value;

        // Load the story using the centralized entity loader (including deleted for delete operations)
        var story = await entityLoader.LoadStoryWithAuthorsAsync(id, includeDeleted: true, cancellationToken: cancellationToken);

        if (story is null)
            return Errors.StoryNotFound;

        // Check if already deleted
        if (story.DeletedAt.HasValue)
            return Errors.AlreadyDeleted;

        // Check authorization - only the owner can delete the story
        if (story.OwnerId != author.Id)
            return Errors.NotAuthorized;

        try
        {
            // Soft delete the story - the interceptor will handle setting DeletedAt
            context.Stories.Remove(story);
            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (DbUpdateException)
        {
            return Errors.DatabaseError;
        }
    }
    public static string EndpointName => nameof(DeleteStory);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapDelete("stories/{id:ulid}", async (
                [FromRoute] Ulid id,
                DeleteStory useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);

                return result.ToDeletedResult();
            })
            .WithSummary("Delete Story")
            .WithDescription("Permanently deletes a story and all its associated content including chapters, " +
                "collaborators, and metadata. This action cannot be undone. Only the story owner can delete a story. " +
                "The story must not have any active collaborators or pending invitations. " +
                "Requires authentication and ownership permissions.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses(conflict: false, validation: false)
            .Produces(StatusCodes.Status204NoContent);
        }
    }
}
