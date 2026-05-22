using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

using MongoDB.Bson;
using MongoDB.Driver;

namespace IHFiction.FictionApi.Stories;

internal sealed class GetPublishedWorkContent(
    IMongoCollection<WorkBody> workBodies,
    FictionDbContext context) : IUseCase, INameEndpoint<GetPublishedWorkContent>
{
    internal sealed record GetPublishedWorkContentQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetPublishedWorkContentResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record GetPublishedWorkContentResponse(
        Ulid Id,
        string WorkType,
        string Title,
        Ulid? StoryId,
        string? StoryTitle,
        Ulid? BookId,
        string? BookTitle,
        ObjectId ContentId,
        string Content,
        string? Note1,
        string? Note2,
        DateTime ContentUpdatedAt,
        DateTime WorkUpdatedAt);

    internal static class Errors
    {
        public static readonly DomainError WorkNotFound = new("WorkContent.NotFound", "Work not found.");
        public static readonly DomainError WorkNotPublished = new("WorkContent.NotPublished", "Work is not published.");
        public static readonly DomainError NotDirectlyReadable = new("WorkContent.NotDirectlyReadable", "Work is not directly readable.");
        public static readonly DomainError ContentNotFound = new("WorkContent.ContentNotFound", "Work content not found.");
        public static readonly DomainError NoContent = new("WorkContent.NoContent", "Work does not have content yet.");
    }

    public async Task<Result<GetPublishedWorkContentResponse>> HandleAsync(
        Ulid id,
        CancellationToken cancellationToken = default)
    {
        var story = await context.Stories
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (story is not null)
        {
            var hasChapters = await context.Chapters
                .AsNoTracking()
                .AnyAsync(c => c.StoryId == story.Id, cancellationToken);
            var hasBooks = await context.Books
                .AsNoTracking()
                .AnyAsync(b => b.StoryId == story.Id, cancellationToken);

            return await GetStoryContentAsync(story, hasChapters, hasBooks, cancellationToken);
        }

        var chapter = await context.Chapters
            .Include(c => c.Story)
            .Include(c => c.Book)
                .ThenInclude(b => b!.Story)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (chapter is not null)
        {
            return await GetChapterContentAsync(chapter, cancellationToken);
        }

        var bookExists = await context.Books
            .AsNoTracking()
            .AnyAsync(b => b.Id == id, cancellationToken);

        return bookExists
            ? Errors.NotDirectlyReadable
            : Errors.WorkNotFound;
    }

    private async Task<Result<GetPublishedWorkContentResponse>> GetStoryContentAsync(
        Story story,
        bool hasChapters,
        bool hasBooks,
        CancellationToken cancellationToken)
    {
        if (!story.IsPublished)
        {
            return Errors.WorkNotPublished;
        }

        if (hasBooks || hasChapters)
        {
            return Errors.NotDirectlyReadable;
        }

        if (!story.HasContent || story.WorkBodyId is null)
        {
            return Errors.NoContent;
        }

        var workBody = await workBodies
            .Find(wb => wb.Id == story.WorkBodyId)
            .FirstOrDefaultAsync(cancellationToken);

        return workBody is null
            ? Errors.ContentNotFound
            : new GetPublishedWorkContentResponse(
                story.Id,
                nameof(Story),
                story.Title,
                story.Id,
                story.Title,
                null,
                null,
                workBody.Id,
                workBody.Content,
                workBody.Note1,
                workBody.Note2,
                workBody.UpdatedAt,
                story.UpdatedAt);
    }

    private async Task<Result<GetPublishedWorkContentResponse>> GetChapterContentAsync(
        Chapter chapter,
        CancellationToken cancellationToken)
    {
        var story = chapter.Story ?? chapter.Book?.Story;
        if (story is null)
        {
            return CommonErrors.Story.NotFound;
        }

        if (!story.IsPublished)
        {
            return CommonErrors.Story.NotPublished;
        }

        if (!chapter.IsPublished)
        {
            return CommonErrors.Chapter.NotPublished;
        }

        if (chapter.WorkBodyId == ObjectId.Empty)
        {
            return Errors.NoContent;
        }

        var workBody = await workBodies
            .Find(wb => wb.Id == chapter.WorkBodyId)
            .FirstOrDefaultAsync(cancellationToken);

        return workBody is null
            ? Errors.ContentNotFound
            : new GetPublishedWorkContentResponse(
                chapter.Id,
                nameof(Chapter),
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

    public static string EndpointName => nameof(GetPublishedWorkContent);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("works/{id:ulid}/content", async (
                [FromRoute] Ulid id,
                [AsParameters] GetPublishedWorkContentQuery query,
                GetPublishedWorkContent useCase,
                LinkService linker,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, cancellationToken);

                return result
                    .WithLinks(linker, GetPublishedWorkContent.EndpointName, values: [new KeyValuePair<string, string?>("id", id.ToString())])
                    .ToOkResult(query);
            })
            .WithSummary("Get Published Work Content")
            .WithDescription("Retrieves body content for a directly readable published work.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(unauthorized: false, conflict: false, validation: false)
            .Produces<Linked<GetPublishedWorkContentResponse>>();
        }
    }
}
