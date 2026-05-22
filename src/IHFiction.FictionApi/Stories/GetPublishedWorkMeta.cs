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

namespace IHFiction.FictionApi.Stories;

internal sealed class GetPublishedWorkMeta(FictionDbContext context) : IUseCase, INameEndpoint<GetPublishedWorkMeta>
{
    internal sealed record GetPublishedWorkMetaQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetPublishedWorkMetaResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal sealed record WorkAuthor(Ulid Id, string Name);

    internal sealed record ReadableWorkItem(
        Ulid Id,
        string Title,
        int Order,
        Ulid? BookId,
        string? BookTitle);

    internal sealed record GetPublishedWorkMetaResponse(
        Ulid Id,
        string WorkType,
        string ReaderKind,
        string Title,
        string Description,
        DateTime? PublishedAt,
        DateTime UpdatedAt,
        bool IsDirectlyReadable,
        bool CanIndex,
        string? CanonicalUrl,
        string PageUrl,
        string? CoverImageUrl,
        Ulid? CoverStoryId,
        Ulid? StoryId,
        string? StoryTitle,
        Ulid? BookId,
        string? BookTitle,
        Ulid? DefaultReadableWorkId,
        IEnumerable<ReadableWorkItem> ReadableChildren,
        IEnumerable<WorkAuthor> Authors);

    internal static class Errors
    {
        public static readonly DomainError WorkNotFound = new("Work.NotFound", "Work not found.");
        public static readonly DomainError WorkNotPublished = new("Work.NotPublished", "Work is not published.");
    }

    public async Task<Result<GetPublishedWorkMetaResponse>> HandleAsync(
        Ulid id,
        CancellationToken cancellationToken = default)
    {
        var story = await LoadStoryAsync(id, cancellationToken);
        if (story is not null)
        {
            return story.IsPublished
                ? BuildStoryMeta(story)
                : Errors.WorkNotPublished;
        }

        var chapter = await LoadChapterAsync(id, cancellationToken);
        if (chapter is not null)
        {
            var readableChildren = await LoadReadableChildrenForChapterAsync(chapter, cancellationToken);
            return BuildChapterMeta(chapter, readableChildren);
        }

        var book = await LoadBookAsync(id, cancellationToken);
        if (book is not null)
        {
            return BuildBookMeta(book);
        }

        return Errors.WorkNotFound;
    }

    private async Task<Story?> LoadStoryAsync(Ulid id, CancellationToken cancellationToken)
    {
        return await context.Stories
            .Include(s => s.Cover)
            .Include(s => s.Owner)
            .Include(s => s.Authors)
            .Include(s => s.Chapters)
            .Include(s => s.Books)
                .ThenInclude(b => b.Chapters)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    private async Task<Chapter?> LoadChapterAsync(Ulid id, CancellationToken cancellationToken)
    {
        return await context.Chapters
            .Include(c => c.Owner)
            .Include(c => c.Authors)
            .Include(c => c.Story)
                .ThenInclude(s => s!.Cover)
            .Include(c => c.Story)
                .ThenInclude(s => s!.Owner)
            .Include(c => c.Story)
                .ThenInclude(s => s!.Authors)
            .Include(c => c.Book)
                .ThenInclude(b => b!.Story)
                    .ThenInclude(s => s.Cover)
            .Include(c => c.Book)
                .ThenInclude(b => b!.Story)
                    .ThenInclude(s => s.Authors)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    private async Task<Book?> LoadBookAsync(Ulid id, CancellationToken cancellationToken)
    {
        return await context.Books
            .Include(b => b.Owner)
            .Include(b => b.Authors)
            .Include(b => b.Story)
                .ThenInclude(s => s.Cover)
            .Include(b => b.Story)
                .ThenInclude(s => s.Authors)
            .Include(b => b.Chapters)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    private static Result<GetPublishedWorkMetaResponse> BuildStoryMeta(Story story)
    {
        var storyType = GetStoryReaderKind(story);
        var isDirectlyReadable = storyType == StoryType.SingleBody;
        var readableChildren = storyType switch
        {
            StoryType.MultiChapter => story.Chapters
                .Where(c => c.BookId is null && c.IsPublished)
                .OrderBy(c => c.Order)
                .Select(c => new ReadableWorkItem(c.Id, c.Title, c.Order, null, null))
                .ToList(),
            StoryType.MultiBook => story.Books
                .Where(b => b.IsPublished)
                .OrderBy(b => b.Order)
                .SelectMany(b => b.Chapters
                    .Where(c => c.IsPublished)
                    .OrderBy(c => c.Order)
                    .Select(c => new ReadableWorkItem(c.Id, c.Title, c.Order, b.Id, b.Title)))
                .ToList(),
            _ => []
        };

        return new GetPublishedWorkMetaResponse(
            story.Id,
            nameof(Story),
            storyType,
            story.Title,
            story.Description,
            story.PublishedAt,
            story.UpdatedAt,
            isDirectlyReadable,
            isDirectlyReadable,
            isDirectlyReadable ? $"/read/{story.Id}" : null,
            $"/read/{story.Id}",
            story.Cover is not null ? $"/stories/{story.Id}/cover" : null,
            story.Cover is not null ? story.Id : null,
            story.Id,
            story.Title,
            null,
            null,
            readableChildren.FirstOrDefault()?.Id,
            readableChildren,
            OrderAuthors(story.Authors, story.OwnerId));
    }

    private async Task<List<ReadableWorkItem>> LoadReadableChildrenForChapterAsync(
        Chapter chapter,
        CancellationToken cancellationToken)
    {
        return chapter.BookId is not null
            ? await context.Chapters
                .Where(c => c.BookId == chapter.BookId && c.PublishedAt != null)
                .OrderBy(c => c.Order)
                .Select(c => new ReadableWorkItem(c.Id, c.Title, c.Order, chapter.BookId, chapter.Book!.Title))
                .AsNoTracking()
                .ToListAsync(cancellationToken)
            : await context.Chapters
                .Where(c => c.StoryId == chapter.StoryId && c.BookId == null && c.PublishedAt != null)
                .OrderBy(c => c.Order)
                .Select(c => new ReadableWorkItem(c.Id, c.Title, c.Order, null, null))
                .AsNoTracking()
                .ToListAsync(cancellationToken);
    }

    private static Result<GetPublishedWorkMetaResponse> BuildChapterMeta(
        Chapter chapter,
        List<ReadableWorkItem> readableChildren)
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

        var description = chapter.Book is null
            ? $"Read {chapter.Title} from {story.Title} on IHeartFiction."
            : $"Read {chapter.Title} from {chapter.Book.Title}, part of {story.Title}, on IHeartFiction.";
        return new GetPublishedWorkMetaResponse(
            chapter.Id,
            nameof(Chapter),
            nameof(Chapter),
            chapter.Title,
            description,
            chapter.PublishedAt,
            chapter.UpdatedAt,
            true,
            true,
            $"/read/{chapter.Id}",
            $"/read/{chapter.Id}",
            story.Cover is not null ? $"/stories/{story.Id}/cover" : null,
            story.Cover is not null ? story.Id : null,
            story.Id,
            story.Title,
            chapter.BookId,
            chapter.Book?.Title,
            null,
            readableChildren,
            OrderAuthors(story.Authors.Count > 0 ? story.Authors : chapter.Authors, story.OwnerId));
    }

    private static Result<GetPublishedWorkMetaResponse> BuildBookMeta(Book book)
    {
        if (!book.IsPublished)
        {
            return CommonErrors.Book.NotPublished;
        }

        if (!book.Story.IsPublished)
        {
            return CommonErrors.Story.NotPublished;
        }

        var readableChildren = book.Chapters
            .Where(c => c.IsPublished)
            .OrderBy(c => c.Order)
            .Select(c => new ReadableWorkItem(c.Id, c.Title, c.Order, book.Id, book.Title))
            .ToList();

        return new GetPublishedWorkMetaResponse(
            book.Id,
            nameof(Book),
            nameof(Book),
            book.Title,
            book.Description,
            book.PublishedAt,
            book.UpdatedAt,
            false,
            false,
            null,
            $"/read/{book.Id}",
            book.Story.Cover is not null ? $"/stories/{book.Story.Id}/cover" : null,
            book.Story.Cover is not null ? book.Story.Id : null,
            book.StoryId,
            book.Story.Title,
            book.Id,
            book.Title,
            readableChildren.FirstOrDefault()?.Id,
            readableChildren,
            OrderAuthors(book.Story.Authors.Count > 0 ? book.Story.Authors : book.Authors, book.Story.OwnerId));
    }

    private static List<WorkAuthor> OrderAuthors(IEnumerable<Data.Authors.Domain.Author> authors, Ulid ownerId)
    {
        return authors
            .Select(a => new WorkAuthor(a.Id, a.Name))
            .OrderBy(a => a.Id, new OwnerFirst(ownerId))
            .ThenBy(a => a.Id)
            .ToList();
    }

    private static string GetStoryReaderKind(Story story)
    {
        return story switch
        {
            { } when story.HasBooks => StoryType.MultiBook,
            { } when story.HasChapters => StoryType.MultiChapter,
            { } when story.HasContent => StoryType.SingleBody,
            _ => StoryType.New
        };
    }

    private sealed class OwnerFirst(Ulid ownerId) : Comparer<Ulid>
    {
        public override int Compare(Ulid x, Ulid y) => (x, y) switch
        {
            { } when x == ownerId && y != ownerId => -1,
            { } when x != ownerId && y == ownerId => 1,
            _ => 0
        };
    }

    public static string EndpointName => nameof(GetPublishedWorkMeta);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("works/{id:ulid}/meta", async (
                [FromRoute] Ulid id,
                [AsParameters] GetPublishedWorkMetaQuery query,
                GetPublishedWorkMeta useCase,
                LinkService linker,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, cancellationToken);

                return result
                    .WithLinks(linker, GetPublishedWorkMeta.EndpointName, values: [new KeyValuePair<string, string?>("id", id.ToString())])
                    .ToOkResult(query);
            })
            .WithSummary("Get Published Work Metadata")
            .WithDescription("Retrieves reader metadata for a published story, book, or chapter.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false, validation: false)
            .Produces<Linked<GetPublishedWorkMetaResponse>>();
        }
    }
}
