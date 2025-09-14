using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Validation;

namespace IHFiction.FictionApi.Stories;

internal sealed class UpdateBookMetadata(
    FictionDbContext context,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<UpdateBookMetadata>
{
    internal static class Errors
    {
        public static readonly DomainError BookNotFound = new("UpdateBookMetadata.NotFound", "The specified book does not exist.");
        public static readonly DomainError ConcurrencyConflict = CommonErrors.Database.ConcurrencyConflict;
        public static readonly DomainError DatabaseError = CommonErrors.Database.SaveFailed;
        public static readonly DomainError TitleExists = new("UpdateBookMetadata.TitleExists", "A book with this title already exists in this story.");
    }

    /// <summary>
    /// Request body model for updating book metadata.
    /// </summary>
    internal sealed record UpdateBookMetadataBody(
        [property: Required(ErrorMessage = "Title is required.")]
        [property: StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
        [property: NoHarmfulContent]
        string Title,

        [property: StringLength(2000, ErrorMessage = "Description must be 2000 characters or less.")]
        [property: NoExcessiveWhitespace(5)]
        [property: NoHarmfulContent]
        string? Description = null
    );

    /// <summary>
    /// Response model for updating book metadata.
    /// </summary>
    internal sealed record UpdateBookMetadataResponse(
        Ulid BookId,
        string Title,
        string? Description,
        DateTime UpdatedAt
    );

    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<UpdateBookMetadataResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    public async Task<Result<UpdateBookMetadataResponse>> HandleAsync(
        Ulid id,
        UpdateBookMetadataBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        // Authorize book access
        var authResult = await authorizationService.AuthorizeBookAccessAsync(
            id, claimsPrincipal, StoryAccessLevel.Edit, cancellationToken: cancellationToken);
        if (authResult.IsFailure) return authResult.DomainError;

        var book = authResult.Value.Book;
        var sanitizedTitle = InputSanitizationService.SanitizeTitle(body.Title);
        var sanitizedDescription = InputSanitizationService.SanitizeDescription(body.Description);

        // Ensure current user is in Authors
        var currentAuthor = authResult.Value.Author;
        if (!book.Authors.Any(a => a.Id == currentAuthor.Id))
        {
            book.Authors.Add(currentAuthor);
        }

        // Check for duplicate title within the same story
        if (book.Title != sanitizedTitle)
        {
            var titleExists = await context.Books.AnyAsync(b => b.StoryId == book.StoryId && b.Title == sanitizedTitle && b.Id != id, cancellationToken);
            if (titleExists)
                return Errors.TitleExists;
        }

        book.Title = sanitizedTitle;
        book.Description = sanitizedDescription;
        book.UpdatedAt = DateTime.UtcNow;

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

        return new UpdateBookMetadataResponse(
            book.Id,
            book.Title,
            book.Description,
            book.UpdatedAt
        );
    }

    public static string EndpointName => nameof(UpdateBookMetadata);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPut("books/{id:ulid}", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                [FromBody] UpdateBookMetadataBody body,
                UpdateBookMetadata useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, body, claimsPrincipal, cancellationToken);
                return result.ToOkResult(query);
            })
            .WithSummary("Update Book Metadata")
            .WithDescription("Updates a book's title, description, and author notes. Only the story owner or authorized collaborators can update book metadata. Requires authentication and appropriate permissions.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses()
            .Produces<Linked<UpdateBookMetadataResponse>>()
            .Accepts<UpdateBookMetadataBody>(MediaTypeNames.Application.Json);
        }
    }
}
