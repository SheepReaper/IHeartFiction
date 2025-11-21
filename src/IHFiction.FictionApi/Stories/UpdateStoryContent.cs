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

using MongoDB.Bson;
using MongoDB.Driver;

namespace IHFiction.FictionApi.Stories;

internal sealed class UpdateStoryContent(
    FictionDbContext context,
    IMongoCollection<WorkBody> workBodies,
    UserService userService,
    TimeProvider dateTimeProvider,
    IOptions<MarkdownOptions> markdownOptions,
    IHostEnvironment environment) : IUseCase, INameEndpoint<UpdateStoryContent>
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly DomainError StoryNotFound = CommonErrors.Story.NotFound;
        public static readonly DomainError AuthorNotFound = CommonErrors.Author.NotRegistered;
        public static readonly DomainError NotAuthorized = CommonErrors.Author.NotAuthorized;
        public static readonly DomainError ConcurrencyConflict = CommonErrors.Database.ConcurrencyConflict;
        public static readonly DomainError DatabaseError = CommonErrors.Database.SaveFailed;

        // Business logic errors specific to content updates
        public static readonly DomainError InvalidStoryStructure = new("UpdateStoryContent.InvalidStoryStructure", "Story has chapters or books and cannot have direct content.");
        public static readonly DomainError ContentNotFound = new("UpdateStoryContent.ContentNotFound", "Story content not found.");
    }

    /// <summary>
    /// Request model for updating a story's content.
    /// </summary>
    /// <param name="Content">The main content of the story in markdown format</param>
    /// <param name="Note1">Optional note field that can contain markdown content for author notes or comments</param>
    /// <param name="Note2">Optional second note field that can contain markdown content for additional author notes</param>
    internal sealed record UpdateStoryContentBody
    (
        [property: Required(ErrorMessage = "Content is required.")]
        [property: StringLength(1000000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 1,000,000 characters.")]
        [property: ValidMarkdown]
        string? Content = null,

        [property: StringLength(5000, ErrorMessage = "Note1 must be 5000 characters or less.")]
        [property: ValidMarkdown]
        string? Note1 = null,

        [property: StringLength(5000, ErrorMessage = "Note2 must be 5000 characters or less.")]
        [property: ValidMarkdown]
        string? Note2 = null
    );

    internal sealed record UpdateStoryContentQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UpdateStoryContentResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for updating a story's content.
    /// </summary>
    /// <param name="StoryId">Unique identifier for the story</param>
    /// <param name="StoryTitle">Title of the story</param>
    /// <param name="ContentId">Unique identifier for the content document</param>
    /// <param name="Content">Updated content of the story in markdown format</param>
    /// <param name="Note1">Updated content of the top author note in markdown format</param>
    /// <param name="Note2">Updated content of the bottom author note in markdown format</param>
    /// <param name="ContentUpdatedAt">When the content was last updated</param>
    /// <param name="StoryUpdatedAt">When the story metadata was last updated</param>
    internal sealed record UpdateStoryContentResponse(
        Ulid StoryId,
        string StoryTitle,
        ObjectId ContentId,
        string Content,
        string? Note1,
        string? Note2,
        DateTime ContentUpdatedAt,
        DateTime StoryUpdatedAt);

    public async Task<Result<UpdateStoryContentResponse>> HandleAsync(
        Ulid id,
        UpdateStoryContentBody body,
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
            .Include(s => s.Authors)
            .Include(s => s.Chapters)
            .Include(s => s.Books)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (story is null)
            return Errors.StoryNotFound;

        // Check authorization - user must be owner or collaborating author
        var isOwner = story.OwnerId == author.Id;
        var isCollaborator = story.Authors.Any(a => a.Id == author.Id);

        if (!isOwner && !isCollaborator)
            return Errors.NotAuthorized;

        // Validate story structure - stories with chapters or books cannot have direct content
        if (story.HasChapters || story.HasBooks)
            return Errors.InvalidStoryStructure;

        // Sanitize input using markdown-aware sanitization
        var options = markdownOptions.Value;
        var isDevelopment = environment.IsDevelopment();

        var sanitizedContent = MarkdownSanitizer.SanitizeContent(body.Content!, options, isDevelopment);
        var sanitizedNote1 = MarkdownSanitizer.SanitizeNote(body.Note1, options, isDevelopment);
        var sanitizedNote2 = MarkdownSanitizer.SanitizeNote(body.Note2, options, isDevelopment);

        // TODO: This needs a strategy to handle the multi-context transactions
        try
        {
            var now = dateTimeProvider.GetUtcNow().UtcDateTime;

            if (story.WorkBodyId == null || story.WorkBodyId == ObjectId.Empty)
                story.WorkBodyId = ObjectId.GenerateNewId();

            var r = await workBodies.UpdateOneAsync(wb => wb.Id == story.WorkBodyId, Builders<WorkBody>.Update
                .Set(wb => wb.Content, sanitizedContent)
                .Set(wb => wb.Note1, sanitizedNote1)
                .Set(wb => wb.Note2, sanitizedNote2)
                .Set(wb => wb.UpdatedAt, now), new UpdateOptions { IsUpsert = true }, cancellationToken: cancellationToken);

            if (!r.IsAcknowledged)
                throw new DbUpdateException("Failed to update story content in the database.");

            // Save changes to both databases
            await context.SaveChangesAsync(cancellationToken);

            return new UpdateStoryContentResponse(
                story.Id,
                story.Title,
                story.WorkBodyId.Value,
                sanitizedContent,
                sanitizedNote1,
                sanitizedNote2,
                now,
                story.UpdatedAt);
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
    public static string EndpointName => nameof(UpdateStoryContent);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPut("stories/{id:ulid}/content", async (
                [FromRoute] Ulid id,
                [AsParameters] UpdateStoryContentQuery query,
                [FromBody] UpdateStoryContentBody body,
                UpdateStoryContent useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, body, claimsPrincipal, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Update Story Content")
            .WithDescription("Updates the content of a story that has direct content (not chapters or books). " +
                "Only story owners and authorized collaborators can update story content. " +
                "The content supports full markdown formatting including images and links with security validation. " +
                "Images must be from approved domains or base64 encoded with size limits. " +
                "Requires authentication and appropriate permissions.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses(conflict: false)
            .Produces<Linked<UpdateStoryContentResponse>>()
            .Accepts<UpdateStoryContentBody>(MediaTypeNames.Application.Json);
        }
    }
}
