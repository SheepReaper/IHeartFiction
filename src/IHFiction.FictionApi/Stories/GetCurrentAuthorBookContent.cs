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

    internal sealed record BookContentChapterItem(
        Ulid Id,
        string Title,
        ObjectId ContentId,
        string Content,
        string? Note1,
        string? Note2,
        DateTime UpdatedAt
    );

    internal sealed record GetCurrentAuthorBookContentResponse(
        Ulid Id,
        string Title,
        Ulid StoryId,
        string StoryTitle,
        IEnumerable<BookContentChapterItem> Chapters,
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

        var chapterBodies = storyDbContext.WorkBodies
            .Where(wb => book.Chapters.Any(c => c.WorkBodyId == wb.Id))
            .AsNoTracking().Join(book.Chapters, wb => wb.Id, c => c.WorkBodyId, (wb, c) => new BookContentChapterItem(
                c.Id,
                c.Title,
                wb.Id,
                wb.Content,
                wb.Note1,
                wb.Note2,
                wb.UpdatedAt
            ));

        return new GetCurrentAuthorBookContentResponse(
            book.Id,
            book.Title,
            book.Story!.Id,
            book.Story.Title,
            chapterBodies,
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
