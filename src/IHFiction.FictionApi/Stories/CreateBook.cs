using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Validation;

using Error = IHFiction.SharedKernel.Infrastructure.DomainError;

namespace IHFiction.FictionApi.Stories;

internal sealed class CreateBook(
    FictionDbContext context,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<CreateBook>
{
    internal static class Errors
    {
        public static readonly Error AuthorNotFound = CommonErrors.Author.NotRegistered;
        public static readonly Error DatabaseError = CommonErrors.Database.SaveFailed;
        public static readonly Error StoryNotFound = new("CreateBook.StoryNotFound", "The specified story does not exist.");
        public static readonly Error NotStoryOwner = new("CreateBook.NotStoryOwner", "Only the owner of the story can add a book.");
        public static readonly Error TitleExists = new("CreateBook.TitleExists", "A book with this title already exists in this story.");
    }

    /// <summary>
    /// Request model for creating a new book.
    /// </summary>
    /// <param name="Title">The title of the book.</param>
    /// <param name="Description">A detailed description of the book.</param>
    internal sealed record CreateBookBody(
        [property: Required(ErrorMessage = "Title is required.")]
        [property: StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
        [property: NoExcessiveWhitespace(3)]
        [property: NoHarmfulContent]
        string? Title = null,

        [property: Required(ErrorMessage = "Description is required.")]
        [property: StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
        [property: NoExcessiveWhitespace(5)]
        [property: NoHarmfulContent]
        string? Description = null
    );

    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<CreateBookResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for a newly created book.
    /// </summary>
    /// <param name="Id">Unique identifier for the created book.</param>
    /// <param name="Title">Title of the created book.</param>
    /// <param name="Description">Description of the created book.</param>
    /// <param name="UpdatedAt">When the book was created/last updated.</param>
    /// <param name="OwnerId">Unique identifier of the book owner.</param>
    /// <param name="StoryId">Unique identifier of the story this book belongs to.</param>
    internal sealed record CreateBookResponse(
        Ulid Id,
        string Title,
        string Description,
        DateTime UpdatedAt,
        Ulid OwnerId,
        Ulid StoryId
    );

    public async Task<Result<CreateBookResponse>> HandleAsync(
        Ulid storyId,
        CreateBookBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        var authorResult = await authorizationService.GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);
        if (authorResult.IsFailure) return authorResult.DomainError;

        var author = authorResult.Value;

        var story = await context.Stories
                                 .Include(s => s.Books)
                                 .FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);

        if (story is null)
            return Errors.StoryNotFound;

        if (story.OwnerId != author.Id)
            return Errors.NotStoryOwner;

        var sanitizedTitle = InputSanitizationService.SanitizeTitle(body.Title!);
        var sanitizedDescription = InputSanitizationService.SanitizeDescription(body.Description!);

        if (story.Books.Any(b => b.Title == sanitizedTitle))
            return Errors.TitleExists;

        var book = new Book
        {
            Title = sanitizedTitle,
            Description = sanitizedDescription,
            Owner = author,
            OwnerId = author.Id,
            StoryId = story.Id
        };

        // Ensure owner/creator is in Authors
        if (!book.Authors.Any(a => a.Id == author.Id))
        {
            book.Authors.Add(author);
        }

        story.Books.Add(book);
        await context.SaveChangesAsync(cancellationToken);

        return new CreateBookResponse(
            book.Id,
            book.Title,
            book.Description,
            book.UpdatedAt,
            book.OwnerId,
            story.Id
        );
    }
    public static string EndpointName => nameof(CreateBook);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;


        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("stories/{id:ulid}/books", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                [FromBody] CreateBookBody body,
                CreateBook useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, body, claimsPrincipal, cancellationToken);

                return result.ToCreatedResult($"/books/{result.Value?.Id}", query);
            })
            .WithSummary("Create Book")
            .WithDescription("Creates a new book within a story.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses()
            .Produces<Linked<CreateBookResponse>>(StatusCodes.Status201Created)
            .Accepts<CreateBookBody>(MediaTypeNames.Application.Json);
        }
    }
}
