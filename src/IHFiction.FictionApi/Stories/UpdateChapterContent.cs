using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using IHFiction.Data.Contexts;
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

internal sealed class UpdateChapterContent(
    FictionDbContext context,
    StoryDbContext storyDbContext,
    AuthorizationService authorizationService,
    TimeProvider dateTimeProvider,
    IOptions<MarkdownOptions> markdownOptions,
    IHostEnvironment environment) : IUseCase, INameEndpoint<UpdateChapterContent>
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly DomainError ConcurrencyConflict = CommonErrors.Database.ConcurrencyConflict;
        public static readonly DomainError DatabaseError = CommonErrors.Database.SaveFailed;

        // Business logic errors specific to chapter content updates
        public static readonly DomainError ContentNotFound = new("UpdateChapterContent.NotFound", "Chapter content not found.");
    }

    /// <summary>
    /// Request model for updating chapter content.
    /// </summary>
    /// <param name="Content">The updated content of the chapter in markdown format</param>
    /// <param name="Note1">Optional note field that can contain additional information about the chapter</param>
    /// <param name="Note2">Optional second note field for additional author notes or comments</param>
    internal sealed record UpdateChapterContentBody(
        [property: Required(ErrorMessage = "Content is required.")]
        [property: StringLength(1000000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 1,000,000 characters.")]
        [property: NoHarmfulContent]
        string? Content = null,

        [property: StringLength(5000, ErrorMessage = "Note1 must be 5000 characters or less.")]
        [property: NoHarmfulContent]
        string? Note1 = null,

        [property: StringLength(5000, ErrorMessage = "Note2 must be 5000 characters or less.")]
        [property: NoHarmfulContent]
        string? Note2 = null
    );

    internal sealed record UpdateChapterContentQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UpdateChapterContentResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for updating chapter content.
    /// </summary>
    /// <param name="ChapterId">Unique identifier of the updated chapter</param>
    /// <param name="ChapterTitle">Title of the chapter</param>
    /// <param name="ContentId">Unique identifier for the content document</param>
    /// <param name="Content">Updated content of the chapter in markdown format</param>
    /// <param name="Note1">Updated content of the top author note in markdown format</param>
    /// <param name="Note2">Updated content of the bottom author note in markdown format</param>
    /// <param name="ContentUpdatedAt">When the content was last updated</param>
    /// <param name="ChapterUpdatedAt">When the chapter metadata was last updated</param>
    internal sealed record UpdateChapterContentResponse(
        Ulid ChapterId,
        string ChapterTitle,
        ObjectId ContentId,
        string Content,
        string? Note1,
        string? Note2,
        DateTime ContentUpdatedAt,
        DateTime ChapterUpdatedAt);

    public async Task<Result<UpdateChapterContentResponse>> HandleAsync(
        Ulid id,
        UpdateChapterContentBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        // Authorize chapter access using centralized authorization service
        var authResult = await authorizationService.AuthorizeChapterAccessAsync(
            id, claimsPrincipal, StoryAccessLevel.Edit, cancellationToken: cancellationToken);
        if (authResult.IsFailure) return authResult.DomainError;

        var authorizationResult = authResult.Value;
        var chapter = authorizationResult.Chapter;

        // Sanitize input using markdown-aware sanitization
        var options = markdownOptions.Value;
        var isDevelopment = environment.IsDevelopment();

        var sanitizedContent = MarkdownSanitizer.SanitizeContent(body.Content, options, isDevelopment);
        var sanitizedNote1 = MarkdownSanitizer.SanitizeNote(body.Note1, options, isDevelopment);
        var sanitizedNote2 = MarkdownSanitizer.SanitizeNote(body.Note2, options, isDevelopment);

        try
        {
            var now = dateTimeProvider.GetUtcNow().UtcDateTime;

            // Get the existing content from MongoDB
            var workBody = await storyDbContext.WorkBodies
                .FirstOrDefaultAsync(wb => wb.Id == chapter.WorkBodyId, cancellationToken);

            if (workBody is null)
                return Errors.ContentNotFound;

            // Update the content
            workBody.Content = sanitizedContent;
            workBody.Note1 = sanitizedNote1;
            workBody.Note2 = sanitizedNote2;
            workBody.UpdatedAt = now;

            // Save changes to both databases
            await storyDbContext.SaveChangesAsync(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return new UpdateChapterContentResponse(
                chapter.Id,
                chapter.Title,
                workBody.Id,
                workBody.Content,
                workBody.Note1,
                workBody.Note2,
                workBody.UpdatedAt,
                chapter.UpdatedAt);
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
    public static string EndpointName => nameof(UpdateChapterContent);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPut("chapters/{id:ulid}/content", async (
                [FromRoute] Ulid id,
                [AsParameters] UpdateChapterContentQuery query,
                [FromBody] UpdateChapterContentBody body,
                UpdateChapterContent useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, body, claimsPrincipal, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Update Chapter Content")
            .WithDescription("Updates the content of an existing chapter with new markdown content and optional notes. " +
                "Only chapter owners and authorized collaborators can update chapter content. " +
                "The content supports full markdown formatting including images and links. " +
                "Requires authentication and appropriate permissions.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses(conflict: false)
            .Produces<Linked<UpdateChapterContentResponse>>()
            .Accepts<UpdateChapterContentBody>(MediaTypeNames.Application.Json);
        }
    }
}
