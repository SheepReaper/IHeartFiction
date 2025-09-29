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
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Markdown;
using IHFiction.SharedKernel.Validation;

using MongoDB.Bson;

namespace IHFiction.FictionApi.Stories;

internal sealed class AddChapterToBook(
    FictionDbContext context,
    StoryDbContext storyDbContext,
    AuthorizationService authorizationService,
    TimeProvider dateTimeProvider,
    IOptions<MarkdownOptions> markdownOptions,
    IHostEnvironment environment) : IUseCase, INameEndpoint<AddChapterToBook>
{
    internal static class Errors
    {
        public static readonly DomainError ConcurrencyConflict = CommonErrors.Database.ConcurrencyConflict;
        public static readonly DomainError DatabaseError = CommonErrors.Database.SaveFailed;
        public static readonly DomainError BookNotFound = new("AddChapterToBook.BookNotFound", "The specified book does not exist.");
        public static readonly DomainError TitleExists = new("AddChapterToBook.TitleExists", "A chapter with this title already exists in the book.");
        public static readonly DomainError NotBookOwner = new("AddChapterToBook.NotBookOwner", "Only the owner of the book can add a chapter.");
    }

    /// <summary>
    /// Request body model for adding a new chapter to a book.
    /// </summary>
    /// <param name="Title">The title of the new chapter.</param>
    /// <param name="Content">The content of the chapter in markdown format.</param>
    /// <param name="Note1">Optional note field that can contain additional information about the chapter.</param>
    /// <param name="Note2">Optional second note field for additional author notes or comments.</param>
    internal sealed record AddChapterToBookBody(
        [property: Required(ErrorMessage = "Title is required.")]
        [property: StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
        [property: NoHarmfulContent]
        string? Title = null,

        [property: Required(ErrorMessage = "Content is required.")]
        [property: StringLength(1000000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 1,000,000 characters.")]
        [property: NoHarmfulContent]
        string? Content = null,

        [property: StringLength(5000, ErrorMessage = "Note1 must be 5000 characters or less.")]
        [property: NoHarmfulContent]
        string? Note1 = null,

        [property: StringLength(5000, ErrorMessage = "Note2 must be 5000 characters or less.")]
        [property: NoHarmfulContent]
        string? Note2 = null);

    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<AddChapterToBookResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for adding a new chapter to a book.
    /// </summary>
    /// <param name="BookId">Unique identifier of the book the chapter was added to.</param>
    /// <param name="BookTitle">Title of the book.</param>
    /// <param name="BookUpdatedAt">When the book was last updated.</param>
    /// <param name="ChapterId">Unique identifier for the created chapter.</param>
    /// <param name="ChapterTitle">Title of the created chapter.</param>
    /// <param name="ChapterCreatedAt">When the chapter was created.</param>
    /// <param name="ChapterPublishedAt">When the chapter was published (null if unpublished).</param>
    /// <param name="ChapterUpdatedAt">When the chapter was last updated.</param>
    /// <param name="ContentId">Unique identifier for the chapter content document.</param>
    /// <param name="Content">The content of the chapter in markdown format.</param>
    /// <param name="Note1">Optional note field that can contain additional information about the chapter.</param>
    /// <param name="Note2">Optional second note field for additional author notes or comments.</param>
    /// <param name="ContentUpdatedAt">When the chapter content was last updated.</param>
    internal sealed record AddChapterToBookResponse(
        Ulid BookId,
        string BookTitle,
        DateTime BookUpdatedAt,
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

    public async Task<Result<AddChapterToBookResponse>> HandleAsync(
        Ulid bookId,
        AddChapterToBookBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var authorResult = await authorizationService.GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);
        if (authorResult.IsFailure) return authorResult.DomainError;

        var author = authorResult.Value;

        var book = await context.Books
                                .Include(b => b.Chapters)
                                .FirstOrDefaultAsync(b => b.Id == bookId, cancellationToken);

        if (book is null)
            return Errors.BookNotFound;

        if (book.OwnerId != author.Id)
            return Errors.NotBookOwner;

        var options = markdownOptions.Value;
        var isDevelopment = environment.IsDevelopment();

        var sanitizedTitle = InputSanitizationService.SanitizeTitle(body.Title!);
        var sanitizedContent = MarkdownSanitizer.SanitizeContent(body.Content, options, isDevelopment);
        var sanitizedNote1 = MarkdownSanitizer.SanitizeNote(body.Note1, options, isDevelopment);
        var sanitizedNote2 = MarkdownSanitizer.SanitizeNote(body.Note2, options, isDevelopment);

        if (book.Chapters.Any(c => string.Equals(c.Title, sanitizedTitle, StringComparison.OrdinalIgnoreCase)))
            return Errors.TitleExists;

        try
        {
            var workBody = new WorkBody
            {
                Content = sanitizedContent,
                Note1 = sanitizedNote1,
                Note2 = sanitizedNote2,
                UpdatedAt = dateTimeProvider.GetUtcNow().UtcDateTime
            };

            storyDbContext.WorkBodies.Add(workBody);
            await storyDbContext.SaveChangesAsync(cancellationToken);

            var chapter = new Chapter
            {
                Title = sanitizedTitle,
                Owner = author,
                OwnerId = author.Id,
                Book = book,
                BookId = book.Id,
                WorkBodyId = workBody.Id
            };

            chapter.Authors.Add(author);
            book.Chapters.Add(chapter);

            await context.SaveChangesAsync(cancellationToken);

            return new AddChapterToBookResponse(
                book.Id,
                book.Title,
                book.UpdatedAt,
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
    public static string EndpointName => nameof(AddChapterToBook);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("books/{id:ulid}/chapters", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                [FromBody] AddChapterToBookBody body,
                AddChapterToBook useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, body, claimsPrincipal, cancellationToken);

                return result.ToCreatedResult($"/chapters/{result.Value?.ChapterId}", query);
            })
            .WithSummary("Add Chapter to Book")
            .WithDescription("Creates a new chapter within a book.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses()
            .Produces<Linked<AddChapterToBookResponse>>(StatusCodes.Status201Created)
            .Accepts<AddChapterToBookBody>(MediaTypeNames.Application.Json);
        }
    }
}
