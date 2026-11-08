using System.ComponentModel.DataAnnotations;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Features.Discovery;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MongoDB.Bson;
using MongoDB.Driver;

namespace IHFiction.FictionApi.Features.Reading;

/// <summary>
/// Use case for retrieving published chapter content for public reading.
/// This endpoint allows anyone to read published chapter content without authentication.
/// </summary>
internal sealed class GetPublishedChapterContent(
    IMongoCollection<WorkBody> workBodies,
    FictionDbContext context) : IUseCase, INameEndpoint<GetPublishedChapterContent>
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

    internal sealed record GetPublishedChapterContentQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetPublishedChapterContentResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for retrieving chapter content.
    /// </summary>
    /// <param name="ChapterId">Unique identifier of the chapter</param>
    /// <param name="ChapterTitle">Title of the chapter</param>
    /// <param name="StoryId">Unique identifier of the parent story</param>
    /// <param name="StoryTitle">Title of the parent story</param>
    /// <param name="BookId">Unique identifier of the parent book, if applicable</param>
    /// <param name="BookTitle">Title of the parent book, if applicable</param>
    /// <param name="ContentId">Unique identifier for the content document</param>
    /// <param name="Content">The chapter content in markdown format</param>
    /// <param name="Note1">Optional author note about the content</param>
    /// <param name="Note2">Optional second author note</param>
    /// <param name="ContentUpdatedAt">When the content was last updated</param>
    /// <param name="ChapterUpdatedAt">When the chapter metadata was last updated</param>
    internal sealed record GetPublishedChapterContentResponse(
        Ulid ChapterId,
        string ChapterTitle,
        Ulid StoryId,
        string StoryTitle,
        Ulid? BookId,
        string? BookTitle,
        ObjectId ContentId,
        string Content,
        string? Note1,
        string? Note2,
        DateTime ContentUpdatedAt,
        DateTime ChapterUpdatedAt);

    public async Task<Result<GetPublishedChapterContentResponse>> HandleAsync(
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

        // Get the content from MongoDB
        var cursor = await workBodies
            .FindAsync(wb => wb.Id == chapter.WorkBodyId, cancellationToken: cancellationToken);

        var workBody = await cursor.FirstOrDefaultAsync(cancellationToken);

        var story = chapter.Story ?? bookStory!.Story;

        return workBody is null
            ? Errors.ContentNotFound
            : new GetPublishedChapterContentResponse(
                chapter.Id,
                chapter.Title,
                story.Id,
                story.Title,
                chapter.BookId,
                chapter.Book?.Title,
                workBody.Id,
                workBody.Content,
                workBody.Note1,
                workBody.Note2,
                workBody.UpdatedAt,
                chapter.UpdatedAt);
    }
    public async Task<(Ulid? PreviousChapterId, Ulid? NextChapterId)> GetAdjacentChapterIdsAsync(
        Ulid chapterId,
        Ulid storyId,
        Ulid? bookId,
        CancellationToken cancellationToken = default)
    {
        var orderedChapterIds = bookId is not null
            ? await context.Chapters
                .AsNoTracking()
                .Where(c => c.BookId == bookId && c.PublishedAt != null)
                .OrderBy(c => c.Order)
                .ThenBy(c => c.Id)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken)
            : await context.Chapters
                .AsNoTracking()
                .Where(c => c.StoryId == storyId && c.BookId == null && c.PublishedAt != null)
                .OrderBy(c => c.Order)
                .ThenBy(c => c.Id)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

        var index = orderedChapterIds.IndexOf(chapterId);
        if (index < 0)
        {
            return (null, null);
        }

        Ulid? previousId = index > 0 ? orderedChapterIds[index - 1] : null;
        Ulid? nextId = index + 1 < orderedChapterIds.Count ? orderedChapterIds[index + 1] : null;
        return (previousId, nextId);
    }

    public static string EndpointName => nameof(GetPublishedChapterContent);

    internal static List<LinkItem> BuildChapterContentLinks(
        LinkService linker,
        GetPublishedChapterContentResponse chapterContent,
        Ulid? previousChapterId,
        Ulid? nextChapterId)
    {
        var storyId = chapterContent.StoryId.ToString();
        var links = new List<LinkItem>
        {
            linker.Create<GetPublishedChapterContent>("self", HttpMethods.Get, [new("id", chapterContent.ChapterId.ToString())]),
            linker.Create<GetPublishedStory>("story", HttpMethods.Get, [new("id", storyId)]),
            linker.Create<ListPublishedStoryChapters>("chapters", HttpMethods.Get, [new("id", storyId)])
        };

        if (previousChapterId is { } prevId)
        {
            links.Add(linker.Create<GetPublishedChapterContent>("previous-chapter", HttpMethods.Get, [new("id", prevId.ToString())]));
        }

        if (nextChapterId is { } nextId)
        {
            links.Add(linker.Create<GetPublishedChapterContent>("next-chapter", HttpMethods.Get, [new("id", nextId.ToString())]));
        }

        return links;
    }

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("chapters/{id:ulid}/content", async (
                [FromRoute] Ulid id,
                [AsParameters] GetPublishedChapterContentQuery query,
                GetPublishedChapterContent useCase,
                LinkService linker,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, cancellationToken);
                if (result.IsFailure)
                {
                    return result.DomainError.ToProblemDetailsResult();
                }

                var (previousId, nextId) = await useCase.GetAdjacentChapterIdsAsync(
                    result.Value.ChapterId,
                    result.Value.StoryId,
                    result.Value.BookId,
                    cancellationToken);

                return result
                    .WithLinks(chapter => BuildChapterContentLinks(linker, chapter, previousId, nextId))
                    .ToOkResult(query);
            })
            .WithSummary("Get Published Chapter Content")
            .WithDescription("Retrieves the content of a published chapter for public reading. " +
                "This is a public endpoint that allows anyone to read published chapter content. " +
                "Returns the markdown content along with any author notes and metadata. " +
                "No authentication required.")
            .WithTags(ApiTags.Chapters)
            .AllowAnonymous() // Public endpoint - no authentication required
            .WithStandardResponses(conflict: false, unauthorized: false, validation: false)
            .Produces<Linked<GetPublishedChapterContentResponse>>();
        }
    }
}
