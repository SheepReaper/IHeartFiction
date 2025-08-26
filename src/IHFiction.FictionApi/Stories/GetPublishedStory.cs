using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;

using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Stories;

internal sealed class GetPublishedStory(EntityLoaderService entityLoader) : IUseCase, INameEndpoint<GetPublishedStory>
{
    internal sealed record Query(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetPublishedStoryResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Represents an author associated with a story.
    /// </summary>
    /// <param name="Id">Unique identifier for the author</param>
    /// <param name="Name">Display name of the author</param>
    internal sealed record StoryAuthor(Ulid Id, string Name);

    /// <summary>
    /// Represents a tag associated with a story.
    /// </summary>
    /// <param name="Category">The category of the tag (e.g., "genre", "theme")</param>
    /// <param name="Subcategory">Optional subcategory for more specific classification</param>
    /// <param name="Value">The actual tag value</param>
    internal sealed record StoryTag(string Category, string? Subcategory, string Value);

    /// <summary>
    /// Response model for getting a specific story by its ID.
    /// </summary>
    /// <param name="Id">Unique identifier for the story</param>
    /// <param name="Title">Title of the story</param>
    /// <param name="Description">Description of the story</param>
    /// <param name="PublishedAt">When the story was published (null if unpublished)</param>
    /// <param name="IsPublished">Whether the story is currently published</param>
    /// <param name="UpdatedAt">When the story was last updated</param>
    /// <param name="CreatedAt">When the story was created</param>
    /// <param name="OwnerId">Unique identifier of the story owner</param>
    /// <param name="OwnerName">Display name of the story owner</param>
    /// <param name="Authors">Collection of authors associated with this story</param>
    /// <param name="Tags">Collection of tags associated with this story</param>
    /// <param name="HasContent">Whether the story has content written</param>
    /// <param name="HasChapters">Whether the story has chapters</param>
    /// <param name="HasBooks">Whether the story has books</param>
    /// <param name="IsValid">Whether the story data is valid</param>
    internal sealed record GetPublishedStoryResponse(
        Ulid Id,
        string Title,
        string Description,
        DateTime? PublishedAt,
        bool IsPublished,
        DateTime UpdatedAt,
        DateTime CreatedAt,
        Ulid OwnerId,
        string OwnerName,
        IEnumerable<StoryAuthor> Authors,
        IEnumerable<StoryTag> Tags,
        bool HasContent,
        bool HasChapters,
        bool HasBooks,
        bool IsValid);

    public async Task<Result<GetPublishedStoryResponse>> HandleAsync(
        Ulid id,
        CancellationToken cancellationToken = default)
    {
        // Load story with full details using the centralized entity loader
        var story = await entityLoader.LoadStoryWithFullDetailsAsync(id, asNoTracking: true, cancellationToken: cancellationToken);

        if (story is null) return CommonErrors.Story.NotFound;

        return !story.IsPublished
            ? CommonErrors.Story.NotPublished
            : new GetPublishedStoryResponse(
            story.Id,
            story.Title,
            story.Description,
            story.PublishedAt,
            story.IsPublished,
            story.UpdatedAt,
            story.CreatedAt,
            story.OwnerId,
            story.Owner.Name,
            story.Authors.Select(a => new StoryAuthor(a.Id, a.Name)),
            story.Tags.Select(t => new StoryTag(t.Category, t.Subcategory, t.Value)),
            story.HasContent,
            story.HasChapters,
            story.HasBooks,
            story.IsValid);
    }
    public static string EndpointName => nameof(GetPublishedStory);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;


        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("stories/{id:ulid}", async (
                [FromRoute] Ulid id,
                [AsParameters] Query query,
                GetPublishedStory useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Get Story by ID")
            .WithDescription("Retrieves detailed information about a specific story including metadata, " +
                "authors, tags, and publication status. This is a public endpoint that does not require authentication.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous()
            .WithStandardResponses(conflict: false, forbidden: false, unauthorized: false, validation: false)
            .Produces<Linked<GetPublishedStoryResponse>>();
        }
    }
}
