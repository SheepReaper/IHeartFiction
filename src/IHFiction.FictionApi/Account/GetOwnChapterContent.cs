using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

using MongoDB.Bson;

namespace IHFiction.FictionApi.Account;

internal sealed class GetOwnChapterContent(
    StoryDbContext storyDbContext,
    FictionDbContext context) : IUseCase, INameEndpoint<GetOwnChapterContent>
{
    internal sealed record GetOwnChapterContentQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetOwnChapterContentResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    internal sealed record GetOwnChapterContentResponse(
        Ulid StoryId,
        string StoryTitle,
        Ulid Id,
        string Title,
        int Order,
        DateTime? PublishedAt,
        DateTime UpdatedAt,
        ObjectId ContentId,
        string Content,
        string? Note1,
        string? Note2,
        DateTime ContentUpdatedAt
    );

    public async Task<Result<GetOwnChapterContentResponse>> HandleAsync(
        Ulid id,
        CancellationToken cancellationToken = default
    )
    {
        var chapter = await context.Chapters
            .Include(c => c.Story)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (chapter is null) return CommonErrors.Chapter.NotFound;

        var story = chapter.Story;

        if  (story is null){
            // Must be a book chapter or an error
            var book = await context.Books
                .Include(b => b.Story)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == chapter.BookId, cancellationToken);

            if (book is null || book.Story is null) return CommonErrors.Book.NotLinkedToStory;

            story = book.Story;
        }

        var workBody = await storyDbContext.WorkBodies
            .AsNoTracking()
            .FirstOrDefaultAsync(wb => wb.Id == chapter.WorkBodyId, cancellationToken);

        if (workBody is null) return CommonErrors.Chapter.NoContent;

        return new GetOwnChapterContentResponse(
            story.Id,
            story.Title,
            chapter.Id,
            chapter.Title,
            chapter.Order,
            chapter.PublishedAt,
            chapter.UpdatedAt,
            workBody.Id,
            workBody.Content,
            workBody.Note1,
            workBody.Note2,
            workBody.UpdatedAt
        );
    }
    public static string EndpointName => nameof(GetOwnChapterContent);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("me/chapters/{id:ulid}/content", async (
                [FromRoute] Ulid id,
                [AsParameters] GetOwnChapterContentQuery query,
                GetOwnChapterContent useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, cancellationToken);

                return result.ToOkResult(query);
            })
                .WithSummary("Get My Chapter Content")
                .WithDescription("Retrieves the content of a chapter owned by the current user, including unpublished works. " +
                    "This is a private endpoint for authors to fetch their own content for editing or review. " +
                    "Requires authentication and author status.")
                .WithTags(ApiTags.Account.CurrentUser)
                .RequireAuthorization("author")
                .WithStandardResponses(conflict: false, validation: false)
                .Produces<Linked<GetOwnChapterContentResponse>>();
        }
    }
}
