using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Validation;

using Error = IHFiction.SharedKernel.Infrastructure.DomainError;
using IHFiction.SharedKernel.Linking;
using System.Net.Mime;

namespace IHFiction.FictionApi.Stories;

internal sealed class UpdateStoryMetadata(
    FictionDbContext context,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<UpdateStoryMetadata>
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly Error ConcurrencyConflict = CommonErrors.Database.ConcurrencyConflict;
        public static readonly Error DatabaseError = CommonErrors.Database.SaveFailed;

        // Keep use case-specific errors
        public static readonly Error NotAuthorized = new("UpdateStoryMetadata.NotAuthorized", "You are not authorized to update this story. Only the story owner or collaborating authors can update it.");
        public static readonly Error TitleExists = new("UpdateStoryMetadata.TitleExists", "A story with this title already exists for this author.");
    }

    /// <summary>
    /// Request model for updating a story's metadata (title and description).
    /// </summary>
    /// <param name="Title">The updated title of the story</param>
    /// <param name="Description">The updated description of the story</param>
    internal sealed record UpdateStoryMetadataBody(
        [property: Required(ErrorMessage = "Title is required.")]
        [property: StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
        [property: NoExcessiveWhitespace(3)]
        [property: NoHarmfulContent]
        string? Title = null,

        [property: Required(ErrorMessage = "Description is required.")]
        [property: StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
        [property: NoExcessiveWhitespace(5)]
        [property: NoHarmfulContent]
        string? Description = null
    );

    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UpdateStoryMetadataResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for updating a story's metadata.
    /// </summary>
    /// <param name="Id">Unique identifier for the updated story</param>
    /// <param name="Title">Updated title of the story</param>
    /// <param name="Description">Updated description of the story</param>
    /// <param name="UpdatedAt">When the story was last updated</param>
    /// <param name="OwnerId">Unique identifier of the story owner</param>
    /// <param name="OwnerName">Display name of the story owner</param>
    internal sealed record UpdateStoryMetadataResponse(
        Ulid Id,
        string Title,
        string Description,
        DateTime UpdatedAt,
        Ulid OwnerId,
        string OwnerName);

    public async Task<Result<UpdateStoryMetadataResponse>> HandleAsync(
        Ulid id,
        UpdateStoryMetadataBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        // Authorize story access using the centralized authorization service
        var authResult = await authorizationService.AuthorizeStoryAccessAsync(
            id, claimsPrincipal, StoryAccessLevel.Edit, cancellationToken: cancellationToken);

        if (authResult.IsFailure) return authResult.DomainError;

        var (story, _, _) = authResult.Value;

        // Sanitize input using the centralized service
        var sanitizedTitle = InputSanitizationService.SanitizeTitle(body.Title);
        var sanitizedDescription = InputSanitizationService.SanitizeDescription(body.Description);

        // Check if the title change would conflict with another story by the same owner
        if (story.Title != sanitizedTitle)
        {
            var existingStory = await context.Stories
                .Where(s => s.OwnerId == story.OwnerId && s.Title == sanitizedTitle && s.Id != id)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingStory is not null)
                return Errors.TitleExists;
        }

        // Update the story
        story.Title = sanitizedTitle;
        story.Description = sanitizedDescription;

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

        return new UpdateStoryMetadataResponse(
            story.Id,
            story.Title,
            story.Description,
            story.UpdatedAt,
            story.OwnerId,
            story.Owner.Name);
    }
    public static string EndpointName => nameof(UpdateStoryMetadata);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPut("stories/{id:ulid}", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                [FromBody] UpdateStoryMetadataBody body,
                UpdateStoryMetadata useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, body, claimsPrincipal, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Update Story Metadata")
            .WithDescription("Updates a story's title and description. Only the story owner or authorized collaborators " +
                "can update story metadata. Requires authentication and appropriate permissions.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses()
            .Produces<Linked<UpdateStoryMetadataResponse>>()
            .Accepts<UpdateStoryMetadataBody>(MediaTypeNames.Application.Json);
        }
    }
}
