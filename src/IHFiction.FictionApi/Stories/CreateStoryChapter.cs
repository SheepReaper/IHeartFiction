using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Markdown;
using IHFiction.SharedKernel.Validation;

using MongoDB.Bson;

namespace IHFiction.FictionApi.Stories;

internal sealed class CreateStoryChapter(
    FictionDbContext context,
    StoryDbContext storyDbContext,
    AuthorizationService authorizationService,
    TimeProvider dateTimeProvider,
    IOptions<MarkdownOptions> markdownOptions,
    IHostEnvironment environment) : IUseCase, INameEndpoint<CreateStoryChapter>
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly DomainError ConcurrencyConflict = CommonErrors.Database.ConcurrencyConflict;
        public static readonly DomainError DatabaseError = CommonErrors.Database.SaveFailed;

        // Business logic errors specific to chapter creation
        public static readonly DomainError InvalidStoryStructure = new("AddChapterToStory.InvalidStoryStructure", "Story has direct content or books and cannot have chapters.");
        public static readonly DomainError TitleExists = new("AddChapterToStory.TitleExists", "A chapter with this title already exists in the story.");
    }

    /// <summary>
    /// Request body model for adding a new chapter to a story.
    /// </summary>
    /// <param name="Title">The title of the new chapter</param>
    /// <param name="Content">The content of the chapter in markdown format</param>
    /// <param name="Note1">Optional note field that can contain additional information about the chapter</param>
    /// <param name="Note2">Optional second note field for additional author notes or comments</param>
    internal sealed record CreateStoryChapterBody(
        [property: Required(ErrorMessage = "Title is required.")]
        [property: StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters and unique amongst chapter titles.")]
        [property: NoHarmfulContent]
        string? Title = null,

        [property: Required(ErrorMessage = "Content is required.")]
        [property: StringLength(1000000, ErrorMessage = "Content must be no more than 1,000,000 characters.")]
        [property: NoHarmfulContent]
        string? Content = null,

        [property: StringLength(5000, ErrorMessage = "Note1 must be 5000 characters or less.")]
        [property: NoHarmfulContent]
        string? Note1 = null,

        [property: StringLength(5000, ErrorMessage = "Note2 must be 5000 characters or less.")]
        [property: NoHarmfulContent]
        string? Note2 = null);

    internal sealed record CreateStoryChapterQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<CreateStoryChapterResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for adding a new chapter to a story.
    /// </summary>
    /// <param name="StoryId">Unique identifier of the story the chapter was added to</param>
    /// <param name="StoryTitle">Title of the story</param>
    /// <param name="StoryUpdatedAt">When the story was last updated</param>
    /// <param name="ChapterId">Unique identifier for the created chapter</param>
    /// <param name="ChapterTitle">Title of the created chapter</param>
    /// <param name="ChapterCreatedAt">When the chapter was created</param>
    /// <param name="ChapterPublishedAt">When the chapter was published (null if unpublished)</param>
    /// <param name="ChapterUpdatedAt">When the chapter was last updated</param>
    /// <param name="ContentId">Unique identifier for the chapter content document</param>
    /// <param name="Content">Content of the chapter in markdown format</param>
    /// <param name="Note1">Optional note field associated with the chapter</param>
    /// <param name="Note2">Optional second note field associated with the chapter</param>
    /// <param name="ContentUpdatedAt">When the chapter content was last updated</param>
    internal sealed record CreateStoryChapterResponse(
        Ulid StoryId,
        string StoryTitle,
        DateTime StoryUpdatedAt,
        Ulid ChapterId,
        string ChapterTitle,
        DateTime ChapterCreatedAt,
        DateTime? ChapterPublishedAt,
        DateTime ChapterUpdatedAt,
        ObjectId ContentId,
        string Content,
        string? Note1,
        string? Note2,
        DateTime ContentUpdatedAt
    );

    public async Task<Result<CreateStoryChapterResponse>> HandleAsync(
        Ulid id,
        CreateStoryChapterBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        // Authorize story access using centralized authorization service
        var authResult = await authorizationService.AuthorizeStoryAccessAsync(
            id, claimsPrincipal, StoryAccessLevel.Edit, includeDeleted: false, cancellationToken);

        if (authResult.IsFailure) return authResult.DomainError;

        // Reload story with additional data needed for chapter creation
        var story = await context.Stories
            .Include(s => s.Authors)
            .Include(s => s.Chapters)
            .Include(s => s.Books)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (story is null)
            return CommonErrors.Story.NotFound;

        // Validate story structure - stories with direct content or books cannot have chapters
        if (story.HasContent || story.HasBooks)
            return Errors.InvalidStoryStructure;

        // Sanitize input using markdown-aware sanitization
        var options = markdownOptions.Value;
        var isDevelopment = environment.IsDevelopment();

        var sanitizedTitle = SanitizeTitle(body.Title!);
        var sanitizedContent = MarkdownSanitizer.SanitizeContent(body.Content, options, isDevelopment);
        var sanitizedNote1 = MarkdownSanitizer.SanitizeNote(body.Note1, options, isDevelopment);
        var sanitizedNote2 = MarkdownSanitizer.SanitizeNote(body.Note2, options, isDevelopment);

        // Check if a chapter with this title already exists in the story
        var existingChapter = story.Chapters.FirstOrDefault(c =>
            string.Equals(c.Title, sanitizedTitle, StringComparison.OrdinalIgnoreCase));

        if (existingChapter is not null)
            return Errors.TitleExists;

        try
        {
            var newWorkBodyId = ObjectId.GenerateNewId();

            // Create the chapter content first
            var workBody = new WorkBody
            {
                Id = newWorkBodyId,
                Content = sanitizedContent,
                Note1 = sanitizedNote1,
                Note2 = sanitizedNote2,
                UpdatedAt = dateTimeProvider.GetUtcNow().UtcDateTime
            };

            storyDbContext.WorkBodies.Add(workBody);

            await storyDbContext.SaveChangesAsync(cancellationToken);

            // Create the chapter
            var chapter = new Chapter
            {
                Title = sanitizedTitle,
                OwnerId = story.OwnerId,
                Story = story,
                WorkBodyId = newWorkBodyId
            };

            // Assign ordering: new chapter should be placed after existing chapters
            chapter.Order = story.Chapters.Select(c => c.Order).DefaultIfEmpty(-1).Max() + 1;

            // Add the author and owner as collaborators on the chapter
            foreach (var existingAuthor in story.Authors) chapter.Authors.Add(existingAuthor);

            // Add the chapter to the story
            story.Chapters.Add(chapter);

            // Save changes to PostgreSQL
            await context.SaveChangesAsync(cancellationToken);

            return new CreateStoryChapterResponse(
                story.Id,
                story.Title,
                story.UpdatedAt,
                chapter.Id,
                chapter.Title,
                chapter.CreatedAt,
                chapter.PublishedAt,
                chapter.UpdatedAt,
                workBody.Id,
                workBody.Content,
                workBody.Note1,
                workBody.Note2,
                workBody.UpdatedAt
                );
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

    private static string SanitizeTitle(string title)
    {
        // Trim and normalize whitespace
        var sanitized = ValidationRegexPatterns.ConsecutiveWhitespace().Replace(title.Trim(), " ");
        return sanitized;
    }
    public static string EndpointName => nameof(CreateStoryChapter);



    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("stories/{id:ulid}/chapters", async (
                [FromRoute] Ulid id,
                [AsParameters] CreateStoryChapterQuery query,
                [FromBody] CreateStoryChapterBody body,
                CreateStoryChapter useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, body, claimsPrincipal, cancellationToken);

                return result.ToCreatedResult($"/chapters/{result.Value?.ChapterId}", query);
            })
            .WithSummary("Add Chapter to Story")
            .WithDescription("Creates a new chapter within a story with the provided title and content. " +
                "Only story owners and authorized collaborators can add chapters. " +
                "The story must support chapters (cannot have direct content or books). " +
                "Chapter titles must be unique within the story. Requires authentication.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses()
            .Produces<Linked<CreateStoryChapterResponse>>(StatusCodes.Status201Created)
            .Accepts<CreateStoryChapterBody>(MediaTypeNames.Application.Json);
        }
    }
}
