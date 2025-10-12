using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Stories;

internal sealed class PublishStory(
    FictionDbContext context,
    UserService userService,
    TimeProvider dateTimeProvider) : IUseCase, INameEndpoint<PublishStory>
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly DomainError StoryNotFound = CommonErrors.Story.NotFound;
        public static readonly DomainError AuthorNotFound = CommonErrors.Author.NotRegistered;
        public static readonly DomainError NotAuthorized = CommonErrors.Author.NotAuthorized;
        public static readonly DomainError ConcurrencyConflict = CommonErrors.Database.ConcurrencyConflict;
        public static readonly DomainError DatabaseError = CommonErrors.Database.SaveFailed;

        // Business logic errors specific to story publishing
        public static readonly DomainError AlreadyPublished = new("PublishStory.AlreadyPublished", "Story is already published.");
        public static readonly DomainError NoContentToPublish = new("PublishStory.NoContentToPublish", "Story has no content to publish. Add content or chapters before publishing.");
        public static readonly DomainError OnlyOwnerCanPublish = new("PublishStory.OnlyOwnerCanPublish", "Only the story owner can publish the story.");
    }

    internal sealed record PublishStoryQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<PublishStoryResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for publishing a story.
    /// </summary>
    /// <param name="StoryId">Unique identifier of the published story</param>
    /// <param name="Title">Title of the story</param>
    /// <param name="Description">Description of the story</param>
    /// <param name="PublishedAt">When the story was published</param>
    /// <param name="UpdatedAt">When the story was last updated</param>
    /// <param name="HasContent">Whether the story has direct content</param>
    /// <param name="HasChapters">Whether the story has chapters</param>
    /// <param name="HasBooks">Whether the story has books</param>
    /// <param name="ChapterCount">Number of chapters in the story</param>
    internal sealed record PublishStoryResponse(
        Ulid StoryId,
        string Title,
        string Description,
        DateTime PublishedAt,
        DateTime UpdatedAt,
        bool HasContent,
        bool HasChapters,
        bool HasBooks,
        int ChapterCount);

    public async Task<Result<PublishStoryResponse>> HandleAsync(
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

        // Check authorization - only the story owner can publish (not collaborators)
        if (story.OwnerId != author.Id)
            return Errors.OnlyOwnerCanPublish;

        // Check if story is already published
        if (story.IsPublished)
            return Errors.AlreadyPublished;

        // Validate that the story has content to publish
        if (!story.HasContent && !story.HasChapters && !story.HasBooks)
            return Errors.NoContentToPublish;

        try
        {
            // Set the published timestamp
            story.PublishedAt = dateTimeProvider.GetUtcNow().UtcDateTime;

            // Save changes
            await context.SaveChangesAsync(cancellationToken);

            return new PublishStoryResponse(
                story.Id,
                story.Title,
                story.Description,
                story.PublishedAt.Value,
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
    public static string EndpointName => nameof(PublishStory);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("stories/{id:ulid}/publish", async (
                [FromRoute] Ulid id,
                [AsParameters] PublishStoryQuery query,
                PublishStory useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Publish Story")
            .WithDescription("Makes a story publicly visible by setting its publication timestamp. " +
                "Only story owners can publish their stories. The story must have content " +
                "(either direct content or chapters) before it can be published. " +
                "Once published, the story becomes discoverable in public listings. " +
                "Requires authentication and story ownership.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses(conflict: false)
            .Produces<Linked<PublishStoryResponse>>();
        }
    }
}
