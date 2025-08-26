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

internal sealed class GetCurrentAuthorStoryContent(
    StoryDbContext storyDbContext,
    EntityLoaderService entityLoader,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<GetCurrentAuthorStoryContent>
{
    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetCurrentAuthorStoryContentResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    internal static class Errors
    {
        public static readonly DomainError StoryNotFound = CommonErrors.Story.NotFound;
        public static readonly DomainError DatabaseError = CommonErrors.Database.ConnectionFailed;
        public static readonly DomainError Forbidden = CommonErrors.Author.NotAuthorized;
        public static readonly DomainError ContentNotFound = new("GetStoryContent.ContentNotFound", "Story content not found.");
    }

    /// <summary>
    /// Represents a summary of a chapter within a story.
    /// </summary>
    /// <param name="Id">Unique identifier for the chapter.</param>
    /// <param name="Title">Title of the chapter.</param>
    internal record ChapterSummary(Ulid Id, string Title);

    /// <summary>
    /// Represents a summary of a book within a story.
    /// </summary>
    /// <param name="Id">Unique identifier for the book.</param>
    /// <param name="Title">Title of the book.</param>
    internal record BookSummary(Ulid Id, string Title);

    /// <summary>
    /// Response model for retrieving the content of a story owned by the current user.
    /// </summary>
    /// <param name="StoryId">Unique identifier for the story.</param>
    /// <param name="StoryTitle">Title of the story.</param>
    /// <param name="StoryDescription">Description of the story.</param>
    /// <param name="IsPublished">Whether the story is published.</param>
    /// <param name="ContentId">Unique identifier for the content document.</param>
    /// <param name="Content">The content of the story.</param>
    /// <param name="Note1">Optional note field that can contain additional information about the story.</param>
    /// <param name="Note2">Optional second note field for additional author notes or comments.</param>
    /// <param name="ContentUpdatedAt">When the content was last updated.</param>
    /// <param name="StoryUpdatedAt">When the story was last updated.</param>
    /// <param name="Chapters">List of chapters within the story.</param>
    /// <param name="Books">List of books within the story.</param>
    internal sealed record GetCurrentAuthorStoryContentResponse(
        Ulid StoryId,
        string StoryTitle,
        string StoryDescription,
        bool IsPublished,
        ObjectId ContentId,
        string? Content,
        string? Note1,
        string? Note2,
        DateTime? ContentUpdatedAt,
        DateTime StoryUpdatedAt,
        ICollection<ChapterSummary> Chapters,
        ICollection<BookSummary> Books
    );

    public async Task<Result<GetCurrentAuthorStoryContentResponse>> HandleAsync(
        Ulid id,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var story = await entityLoader.LoadStoryForConversionAsync(id, cancellationToken);

        if (story is null)
            return Errors.StoryNotFound;

        var authorResult = await authorizationService.GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);

        if (authorResult.IsFailure) return authorResult.DomainError;

        var author = authorResult.Value;

        if (author is null || !story.Authors.Any(a => a.Id == author.Id) && story.OwnerId != author.Id)
            return Errors.Forbidden;

        string? content = null;
        string? note1 = null;
        string? note2 = null;
        DateTime? contentUpdatedAt = null;
        ObjectId contentId = new();

        if (story.WorkBodyId is not null)
        {
            var body = await storyDbContext.WorkBodies.FirstOrDefaultAsync(b => b.Id == story.WorkBodyId, cancellationToken);

            if (body is null)
                return Errors.ContentNotFound;

            content = body.Content;
            note1 = body.Note1;
            note2 = body.Note2;
            contentUpdatedAt = body.UpdatedAt;
            contentId = body.Id;
        }

        var chapterSummaries = story.Chapters.Select(c => new ChapterSummary(c.Id, c.Title)).ToList();
        var bookSummaries = story.Books.Select(b => new BookSummary(b.Id, b.Title)).ToList();

        return new GetCurrentAuthorStoryContentResponse(
            story.Id,
            story.Title,
            story.Description,
            story.IsPublished,
            contentId,
            content,
            note1,
            note2,
            contentUpdatedAt,
            story.UpdatedAt,
            chapterSummaries,
            bookSummaries
        );
    }
    public static string EndpointName => nameof(GetCurrentAuthorStoryContent);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("me/stories/{id:ulid}/content", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                GetCurrentAuthorStoryContent useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, claimsPrincipal, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Get My Story Content")
            .WithDescription("Retrieves the content of a story owned by the current user, including unpublished works. " +
                "This is a private endpoint for authors to fetch their own content for editing or review. " +
                "Requires authentication and author status.")
            .WithTags(ApiTags.Account.CurrentUser)
            .RequireAuthorization("author")
            .WithStandardResponses(conflict: false, validation: false)
            .Produces<Linked<GetCurrentAuthorStoryContentResponse>>();
        }
    }
}
