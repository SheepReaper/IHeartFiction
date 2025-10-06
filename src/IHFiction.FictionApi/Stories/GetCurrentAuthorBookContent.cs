using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

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

internal sealed class GetCurrentAuthorBookContent(
    StoryDbContext storyDbContext,
    FictionDbContext context,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<GetCurrentAuthorBookContent>
{
    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetCurrentAuthorBookContentResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Represents a chapter item within the book content response.
    /// </summary>
    /// <param name="Id">The unique identifier of the chapter.</param>
    /// <param name="Title">The title of the chapter.</param>
    /// <param name="Order">The display order of the chapter.</param>
    /// <param name="ChapterPublishedAt">The date and time when the chapter was published.</param>
    /// <param name="ChapterUpdatedAt">The date and time when the chapter metadata was last updated.</param>
    /// <param name="ContentId">The unique identifier for the content document.</param>
    /// <param name="Content">The chapter content in markdown format.</param>
    /// <param name="Note1">Optional author note about the content.</param>
    /// <param name="Note2">Optional second author note.</param>
    /// <param name="ContentUpdatedAt">The date and time when the content was last updated.</param>
    internal sealed record BookContentChapterItem(
        Ulid Id,
        string Title,
        int Order,
        DateTime? ChapterPublishedAt,
        DateTime ChapterUpdatedAt,
        ObjectId ContentId,
        string Content,
        string? Note1,
        string? Note2,
        DateTime ContentUpdatedAt
    );

    /// <summary>
    /// Response model for retrieving the content of a book owned by the current user.
    /// </summary>
    /// <param name="Id">Unique identifier for the book.</param>
    /// <param name="Title">The title of the book.</param>
    /// <param name="Description">A detailed description of the book.</param>
    /// <param name="Order">The display order of the book within the story.</param>
    /// <param name="StoryId">The unique identifier of the parent story.</param>
    /// <param name="StoryTitle">The title of the parent story.</param>
    /// <param name="Chapters">Collection of chapters within the book.</param>
    /// <param name="PublishedAt">When the book was published (null if unpublished).</param>
    /// <param name="UpdatedAt">When the book was last updated.</param>
    internal sealed record GetCurrentAuthorBookContentResponse(
        Ulid Id,
        string Title,
        string Description,
        int Order,
        Ulid StoryId,
        string StoryTitle,
        IEnumerable<BookContentChapterItem> Chapters,
        DateTime? PublishedAt,
        DateTime UpdatedAt
    );

    public async Task<Result<GetCurrentAuthorBookContentResponse>> HandleAsync(
        Ulid id,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default
    )
    {
        var author = await authorizationService.GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);

        if (author.IsFailure) return author.DomainError;

        var book = await context.Books
            .Include(b => b.Story)
            .Include(b => b.Chapters)
            .Include(b => b.Authors)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (book is null) return CommonErrors.Book.NotFound;

        if (!book.Authors.Any(b => b.Id == author.Value.Id)) return CommonErrors.Book.NotAuthorized;

        // Materialize chapters and join in-memory to avoid EF Core translation issues
        var chapters = book.Chapters.ToList();
        var workBodyIds = chapters.Select(c => c.WorkBodyId).ToList();
        var workBodies = await storyDbContext.WorkBodies
            .Where(wb => workBodyIds.Contains(wb.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var chapterBodies = chapters.Join(workBodies, c => c.WorkBodyId, wb => wb.Id, (c, wb) => new BookContentChapterItem(
            c.Id,
            c.Title,
            c.Order,
            c.PublishedAt,
            c.UpdatedAt,
            wb.Id,
            wb.Content,
            wb.Note1,
            wb.Note2,
            wb.UpdatedAt
        ));

        return new GetCurrentAuthorBookContentResponse(
            book.Id,
            book.Title,
            book.Description ?? string.Empty,
            book.Order,
            book.Story!.Id,
            book.Story.Title,
            chapterBodies,
            book.PublishedAt,
            book.UpdatedAt
        );
    }
    public static string EndpointName => nameof(GetCurrentAuthorBookContent);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("me/books/{id:ulid}/content", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                GetCurrentAuthorBookContent useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Get My Book Content")
            .WithDescription("Retrieves the content of a book owned by the current user, including unpublished works. " +
                "This is a private endpoint for authors to fetch their own content for editing or review. " +
                "Requires authentication and author status.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization("author")
            .WithStandardResponses(conflict: false, validation: false)
            .Produces<Linked<GetCurrentAuthorBookContentResponse>>();
        }
    }
}
