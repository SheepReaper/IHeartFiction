using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

using MongoDB.Bson;

namespace IHFiction.FictionApi.Stories;

/// <summary>
/// Use case for retrieving published chapter content for public reading.
/// This endpoint allows anyone to read published chapter content without authentication.
/// </summary>
internal sealed class GetChapterContent(
    StoryDbContext storyDbContext,
    FictionDbContext context) : IUseCase, INameEndpoint<GetChapterContent>
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly DomainError StoryNotFound = CommonErrors.Story.NotFound;
        public static readonly DomainError DatabaseError = CommonErrors.Database.ConnectionFailed;

        // Business logic errors specific to chapter content retrieval
        public static readonly DomainError ChapterNotFound = new("GetChapterContent.ChapterNotFound", "Chapter not found.");
        public static readonly DomainError ChapterNotPublished = new("GetChapterContent.ChapterNotPublished", "Chapter is not published and cannot be accessed.");
        public static readonly DomainError StoryNotPublished = new("GetChapterContent.StoryNotPublished", "Story is not published and cannot be accessed.");
        public static readonly DomainError ContentNotFound = new("GetChapterContent.ContentNotFound", "Chapter content not found.");
        public static readonly DomainError NoContent = new("GetChapterContent.NoContent", "Chapter does not have content yet.");
    }

    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetChapterContentResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for retrieving chapter content.
    /// </summary>
    /// <param name="ChapterId">Unique identifier of the chapter</param>
    /// <param name="ChapterTitle">Title of the chapter</param>
    /// <param name="StoryId">Unique identifier of the parent story</param>
    /// <param name="StoryTitle">Title of the parent story</param>
    /// <param name="BookId">Unique identifier of the parent book, if applicable</param>
    /// <param name="ContentId">Unique identifier for the content document</param>
    /// <param name="Content">The chapter content in markdown format</param>
    /// <param name="Note1">Optional author note about the content</param>
    /// <param name="Note2">Optional second author note</param>
    /// <param name="ContentUpdatedAt">When the content was last updated</param>
    /// <param name="ChapterUpdatedAt">When the chapter metadata was last updated</param>
    internal sealed record GetChapterContentResponse(
        Ulid ChapterId,
        string ChapterTitle,
        Ulid StoryId,
        string StoryTitle,
        Ulid BookId,
        ObjectId ContentId,
        string Content,
        string? Note1,
        string? Note2,
        DateTime ContentUpdatedAt,
        DateTime ChapterUpdatedAt);

    public async Task<Result<GetChapterContentResponse>> HandleAsync(
        Ulid id,
        CancellationToken cancellationToken = default)
    {
        // Load the chapter with its story
        var chapter = await context.Chapters
            .Include(c => c.Story)
            .Include(c => c.Book)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (chapter is null)
            return Errors.ChapterNotFound;

        var bookStory = chapter.Book is not null ? await context.Books
            .Include(b => b.Story)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == chapter.BookId, cancellationToken) : null;

        if (chapter.Story is null && bookStory is null)
            return Errors.StoryNotFound;

        // Only allow access to published chapters from published stories
        if (!((chapter.Story?.IsPublished ?? false) || (bookStory?.Story?.IsPublished ?? false)))
            return Errors.StoryNotPublished;

        if (chapter.PublishedAt is null)
            return Errors.ChapterNotPublished;

        // Check if chapter has content
        if (chapter.WorkBodyId?.Timestamp == default)
            return Errors.NoContent;

        // Get the content from MongoDB
        var workBody = await storyDbContext.WorkBodies
            .AsNoTracking()
            .FirstOrDefaultAsync(wb => wb.Id == chapter.WorkBodyId, cancellationToken);

        var story = chapter.Story ?? bookStory!.Story!;

        return workBody is null
            ? Errors.ContentNotFound
            : new GetChapterContentResponse(
                chapter.Id,
                chapter.Title,
                story.Id,
                story.Title,
                chapter.BookId ?? Ulid.Empty,
                workBody.Id,
                workBody.Content,
                workBody.Note1,
                workBody.Note2,
                workBody.UpdatedAt,
                chapter.UpdatedAt);
    }
    public static string EndpointName => nameof(GetChapterContent);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;


        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("chapters/{id:ulid}/content", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                GetChapterContent useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Get Published Chapter Content")
            .WithDescription("Retrieves the content of a published chapter for public reading. " +
                "This is a public endpoint that allows anyone to read published chapter content. " +
                "Returns the markdown content along with any author notes and metadata. " +
                "No authentication required.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous() // Public endpoint - no authentication required
            .WithStandardResponses(conflict: false, unauthorized: false, validation: false)
            .Produces<Linked<GetChapterContentResponse>>();
        }
    }
}
