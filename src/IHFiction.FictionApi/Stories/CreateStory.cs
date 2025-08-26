using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Validation;

using Error = IHFiction.SharedKernel.Infrastructure.DomainError;
using System.Net.Mime;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Stories;

internal sealed class CreateStory(
    FictionDbContext context,
    StoryDbContext storyDbContext,
    AuthorizationService authorizationService) : IUseCase, INameEndpoint<CreateStory>
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly Error AuthorNotFound = CommonErrors.Author.NotRegistered;
        public static readonly Error DatabaseError = CommonErrors.Database.SaveFailed;

        // Keep business logic errors specific to this use case
        public static readonly Error TitleExists = new("CreateStory.TitleExists", "A story with this title already exists for this author.");
    }

    /// <summary>
    /// Request model for creating a new story.
    /// </summary>
    /// <param name="Title">The title of the story</param>
    /// <param name="Description">A detailed description of the story</param>
    /// <param name="StoryType">The physical structure of the story</param>
    internal sealed record CreateStoryBody(
        [property: Required(ErrorMessage = "Title is required.")]
        [property: StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
        [property: NoExcessiveWhitespace(3)]
        [property: NoHarmfulContent]
        string? Title = null,

        [property: Required(ErrorMessage = "Description is required.")]
        [property: StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
        [property: NoExcessiveWhitespace(5)]
        [property: NoHarmfulContent]
        string? Description = null,

        [property: Required(ErrorMessage = "StoryType is required.")]
        [property: AllowedValues([StoryType.SingleBody, StoryType.MultiChapter, StoryType.MultiBook], ErrorMessage = "Invalid value for StoryType.")]
        string? StoryType = StoryType.SingleBody
    );

    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<CreateStoryResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Response model for creating a new story.
    /// </summary>
    /// <param name="Id">Unique identifier for the created story</param>
    /// <param name="Title">Title of the created story</param>
    /// <param name="Description">Description of the created story</param>
    /// <param name="UpdatedAt">When the story was created/last updated</param>
    /// <param name="OwnerId">Unique identifier of the story owner</param>
    /// <param name="OwnerName">Display name of the story owner</param>
    internal sealed record CreateStoryResponse(
        Ulid Id,
        string Title,
        string Description,
        DateTime UpdatedAt,
        Ulid OwnerId,
        string OwnerName);

    public async Task<Result<CreateStoryResponse>> HandleAsync(
        CreateStoryBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        // Get the current author using the centralized authorization service
        var authorResult = await authorizationService.GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);
        if (authorResult.IsFailure) return authorResult.DomainError;

        var author = authorResult.Value;

        // Sanitize input using the centralized service
        var sanitizedTitle = InputSanitizationService.SanitizeTitle(body.Title);
        var sanitizedDescription = InputSanitizationService.SanitizeDescription(body.Description);

        // Check if author already has a story with this title
        var existingStory = await context.Stories
            .Where(s => s.OwnerId == author.Id && s.Title == sanitizedTitle)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingStory is not null)
            return Errors.TitleExists;

        // Create the new story
        var story = new Story
        {
            Title = sanitizedTitle,
            Description = sanitizedDescription,
            Owner = author,
            OwnerId = author.Id
        };

        if (body.StoryType == StoryType.SingleBody)
        {
            var workBody = new WorkBody() { Content = string.Empty };
            storyDbContext.WorkBodies.Add(workBody);
            story.WorkBodyId = workBody.Id;
        }

        // Add the author as a collaborator
        story.Authors.Add(author);

        // Save the story - exceptions will be handled by global exception handling
        context.Stories.Add(story);
        await storyDbContext.SaveChangesAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return new CreateStoryResponse(
            story.Id,
            story.Title,
            story.Description,
            story.UpdatedAt,
            story.OwnerId,
            author.Name);
    }
    public static string EndpointName => nameof(CreateStory);


    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("stories", async (
                [AsParameters] Query query,
                [FromBody] CreateStoryBody body,
                CreateStory useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(body, claimsPrincipal, cancellationToken);

                return result.ToCreatedResult($"/stories/{result.Value?.Id}", query);
            })
            .WithSummary("Create Story")
            .WithDescription("Creates a new story with the provided title and description. " +
                "The authenticated user becomes the owner of the story and can manage its content, " +
                "collaborators, and publication status. Requires authentication.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author")
            .WithStandardResponses(notFound: false)
            .Produces<Linked<CreateStoryResponse>>(StatusCodes.Status201Created)
            .Accepts<CreateStoryBody>(MediaTypeNames.Application.Json);
        }
    }
}
