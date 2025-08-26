using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Validation;
using IHFiction.SharedKernel.Linking;
using System.Net.Mime;

namespace IHFiction.FictionApi.Stories;

internal sealed class UpdateChapterMetadata(
    FictionDbContext context,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<UpdateChapterMetadata>
{
    internal static class Errors
    {
        public static readonly DomainError ChapterNotFound = new("UpdateChapterMetadata.NotFound", "The specified chapter does not exist.");
        public static readonly DomainError ConcurrencyConflict = CommonErrors.Database.ConcurrencyConflict;
        public static readonly DomainError DatabaseError = CommonErrors.Database.SaveFailed;
        public static readonly DomainError TitleExists = new("UpdateChapterMetadata.TitleExists", "A chapter with this title already exists.");
    }

    /// <summary>
    /// Request body model for updating chapter metadata.
    /// </summary>
    /// <param name="Title">The new title for the chapter.</param>
    internal sealed record UpdateChapterMetadataBody(
        [property: Required(ErrorMessage = "Title is required.")]
        [property: StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
        [property: NoHarmfulContent]
        string Title
    );

    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UpdateChapterMetadataResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for updating chapter metadata.
    /// </summary>
    /// <param name="ChapterId">Unique identifier for the updated chapter.</param>
    /// <param name="ChapterTitle">New title of the chapter.</param>
    /// <param name="UpdatedAt">When the chapter was last updated.</param>
    internal sealed record UpdateChapterMetadataResponse(
        Ulid ChapterId,
        string ChapterTitle,
        DateTime UpdatedAt
    );

    public async Task<Result<UpdateChapterMetadataResponse>> HandleAsync(
        Ulid id,
        UpdateChapterMetadataBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var authResult = await authorizationService.AuthorizeChapterAccessAsync(id, claimsPrincipal, StoryAccessLevel.Edit, cancellationToken: cancellationToken);
        if (authResult.IsFailure) return authResult.DomainError;

        var chapter = authResult.Value.Chapter;

        var sanitizedTitle = InputSanitizationService.SanitizeTitle(body.Title);

        if (chapter.Title != sanitizedTitle)
        {
            bool titleExists;
            if (chapter.BookId.HasValue)
            {
                titleExists = await context.Chapters.AnyAsync(c => c.BookId == chapter.BookId && c.Title == sanitizedTitle && c.Id != id, cancellationToken);
            }
            else
            {
                titleExists = await context.Chapters.AnyAsync(c => c.StoryId == chapter.StoryId && c.BookId == null && c.Title == sanitizedTitle && c.Id != id, cancellationToken);
            }

            if (titleExists)
                return Errors.TitleExists;
        }

        chapter.Title = sanitizedTitle;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Errors.ConcurrencyConflict;
        }
        catch (DbUpdateException)
        {
            return Errors.DatabaseError;
        }

        return new UpdateChapterMetadataResponse(
            chapter.Id,
            chapter.Title,
            chapter.UpdatedAt);
    }
    public static string EndpointName => nameof(UpdateChapterMetadata);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;


        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPut("chapters/{id:ulid}", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                [FromBody] UpdateChapterMetadataBody body,
                UpdateChapterMetadata useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, body, claimsPrincipal, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Update Chapter Metadata")
            .WithDescription("Updates the metadata of an existing chapter.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses()
            .Produces<Linked<UpdateChapterMetadataResponse>>()
            .Accepts<UpdateChapterMetadataBody>(MediaTypeNames.Application.Json);
        }
    }
}
