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

internal sealed class GetCurrentAuthorChapterContent(
    StoryDbContext storyDbContext,
    FictionDbContext context) : IUseCase, INameEndpoint<GetCurrentAuthorChapterContent>
{
    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetCurrentAuthorChapterContentResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    internal sealed record GetCurrentAuthorChapterContentResponse(
        Ulid Id,
        string Title,
        Ulid StoryId,
        string StoryTitle,
        ObjectId ContentId,
        string Content,
        string? Note1,
        string? Note2,
        DateTime ContentUpdatedAt,
        DateTime UpdatedAt
    );

    public async Task<Result<GetCurrentAuthorChapterContentResponse>> HandleAsync(
        Ulid id,
        CancellationToken cancellationToken = default
    )
    {
        var chapter = await context.Chapters
            .Include(c => c.Story)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (chapter is null) return CommonErrors.Chapter.NotFound;

        var workBody = await storyDbContext.WorkBodies
            .AsNoTracking()
            .FirstOrDefaultAsync(wb => wb.Id == chapter.WorkBodyId, cancellationToken);

        if (workBody is null) return CommonErrors.Chapter.NoContent;

        return new GetCurrentAuthorChapterContentResponse(
            chapter.Id,
            chapter.Title,
            chapter.Story!.Id,
            chapter.Story.Title,
            workBody.Id,
            workBody.Content,
            workBody.Note1,
            workBody.Note2,
            workBody.UpdatedAt,
            chapter.UpdatedAt
        );
    }
    public static string EndpointName => nameof(GetCurrentAuthorChapterContent);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;


        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("me/chapters/{id:ulid}/content", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                GetCurrentAuthorChapterContent useCase,
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
                .Produces<Linked<GetCurrentAuthorChapterContentResponse>>();
        }
    }
}
