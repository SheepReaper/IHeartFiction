using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Authors;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Stories;

internal sealed class UnpublishStory(
    FictionDbContext context,
    UserService userService) : IUseCase, INameEndpoint<UnpublishStory>
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly DomainError StoryNotFound = CommonErrors.Story.NotFound;
        public static readonly DomainError AuthorNotFound = CommonErrors.Author.NotRegistered;
        public static readonly DomainError NotAuthorized = CommonErrors.Author.NotAuthorized;
        public static readonly DomainError ConcurrencyConflict = CommonErrors.Database.ConcurrencyConflict;
        public static readonly DomainError DatabaseError = CommonErrors.Database.SaveFailed;

        // Business logic errors specific to story unpublishing
        public static readonly DomainError NotPublished = new("UnpublishStory.NotPublished", "Story is not currently published.");
        public static readonly DomainError OnlyOwnerCanUnpublish = new("UnpublishStory.OnlyOwnerCanUnpublish", "Only the story owner can unpublish the story.");
    }

    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UnpublishStoryResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for unpublishing a story.
    /// </summary>
    /// <param name="StoryId">Unique identifier of the unpublished story</param>
    /// <param name="Title">Title of the story</param>
    /// <param name="Description">Description of the story</param>
    /// <param name="UpdatedAt">When the story was last updated</param>
    /// <param name="HasContent">Whether the story has direct content</param>
    /// <param name="HasChapters">Whether the story has chapters</param>
    /// <param name="HasBooks">Whether the story has books</param>
    /// <param name="ChapterCount">Number of chapters in the story</param>
    internal sealed record UnpublishStoryResponse(
        Ulid StoryId,
        string Title,
        string Description,
        DateTime UpdatedAt,
        bool HasContent,
        bool HasChapters,
        bool HasBooks,
        int ChapterCount);

    public async Task<Result<UnpublishStoryResponse>> HandleAsync(
        Ulid id,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        // Get the current author
        var authorResult = await userService.GetAuthorAsync(claimsPrincipal, cancellationToken);
        if (authorResult.IsFailure) return Errors.AuthorNotFound;

        var author = authorResult.Value;

        // Get the story with related data
        var story = await context.Stories
            .Include(s => s.Owner)
            .Include(s => s.Chapters)
            .Include(s => s.Books)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (story is null)
            return Errors.StoryNotFound;

        // Check authorization - only the story owner can unpublish (not collaborators)
        if (story.OwnerId != author.Id)
            return Errors.OnlyOwnerCanUnpublish;

        // Check if story is currently published
        if (!story.IsPublished)
            return Errors.NotPublished;

        try
        {
            // Remove the published timestamp (unpublish the story)
            story.PublishedAt = null;

            // Save changes
            await context.SaveChangesAsync(cancellationToken);

            return new UnpublishStoryResponse(
                story.Id,
                story.Title,
                story.Description,
                story.UpdatedAt,
                story.HasContent,
                story.HasChapters,
                story.HasBooks,
                story.Chapters.Count);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Errors.ConcurrencyConflict;
        }
        catch (DbUpdateException)
        {
            return Errors.DatabaseError;
        }
    }
    public static string EndpointName => nameof(UnpublishStory);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;



        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("stories/{id:ulid}/unpublish", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                UnpublishStory useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Unpublish Story")
            .WithDescription("Removes a story from public visibility by clearing its publication timestamp. " +
                "Only story owners can unpublish their stories. The story must currently be published " +
                "to be unpublished. Once unpublished, the story is no longer discoverable in public " +
                "listings but remains accessible to the owner and collaborators. " +
                "Requires authentication and story ownership.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses(conflict: false)
            .Produces<Linked<UnpublishStoryResponse>>();
        }
    }
}
