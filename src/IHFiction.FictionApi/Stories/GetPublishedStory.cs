using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;

using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Stories;

internal sealed class GetPublishedStory(EntityLoaderService entityLoader) : IUseCase, INameEndpoint<GetPublishedStory>
{
    internal sealed record GetPublishedStoryQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetPublishedStoryResponse>]
        string Fields = ""
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
    /// Represents a book associated with a story.
    /// </summary>
    /// <param name="Id">Unique identifier for the book</param>
    /// <param name="Title">Title of the book</param>
    /// <param name="Description">Description of the book</param>
    /// <param name="Order">Order of the book within the story</param>
    /// <param name="Chapters">Collection of chapters within the book</param>
    internal sealed record BookItem(
        Ulid Id,
        string Title,
        string Description,
        int Order,
        IEnumerable<ChapterItem> Chapters
    );

    /// <summary>
    /// Represents a chapter within a book.
    /// </summary>
    /// <param name="Id">Unique identifier for the chapter</param>
    /// <param name="Title">Title of the chapter</param>
    /// <param name="Order">Order of the chapter within the book</param>

    internal sealed record ChapterItem(
        Ulid Id,
        string Title,
        int Order
    );

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
    /// <param name="Type">The type of the story (e.g., "SingleBody", "MultiChapter", "MultiBook")</param>
    /// <param name="Authors">Collection of authors associated with this story</param>
    /// <param name="Tags">Collection of tags associated with this story</param>
    /// <param name="Books">Collection of books within this story (if applicable)</param>
    /// <param name="Chapters">Collection of chapters within this story (if applicable)</param>
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
        string Type,
        IEnumerable<StoryAuthor> Authors,
        IEnumerable<StoryTag> Tags,
        IEnumerable<BookItem> Books,
        IEnumerable<ChapterItem> Chapters
    );

    public async Task<Result<GetPublishedStoryResponse>> HandleAsync(
        Ulid id,
        CancellationToken cancellationToken = default)
    {
        // Load story with full details using the centralized entity loader
        var story = await entityLoader.LoadStoryWithFullDetailsAsync(id, asNoTracking: true, cancellationToken: cancellationToken);

        if (story is null) return CommonErrors.Story.NotFound;

        if (!story.IsPublished)
            return CommonErrors.Story.NotPublished;

        var storyType = GetCurrentStoryType(story);

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
            storyType,
            story.Authors
                .Select(a => new StoryAuthor(a.Id, a.Name))
                .OrderBy(a => a.Id, new OwnerFirst(story.OwnerId))
                .ThenBy(a => a.Id),
            story.Tags
                .OrderBy(t => t.Value)
                .Select(t => new StoryTag(t.Category, t.Subcategory, t.Value)),
            story.Books
                .Where(b => b.IsPublished)
                .OrderBy(b => b.Order)
                .Select(b => new BookItem(b.Id, b.Title, b.Description, b.Order, b.Chapters
                    .Where(c => c.IsPublished)
                    .OrderBy(c => c.Order)
                    .Select(c => new ChapterItem(c.Id, c.Title, c.Order)))),
            story.Chapters
                .Where(c => c.BookId == null && c.IsPublished)
                .OrderBy(c => c.Order)
                .Select(c => new ChapterItem(c.Id, c.Title, c.Order))
            );
    }


    private sealed class OwnerFirst(Ulid ownerId): Comparer<Ulid>
    {
        public override int Compare(Ulid x, Ulid y) => (x, y) switch
        {
            { } when x == ownerId && y != ownerId => -1,
            { } when x != ownerId && y == ownerId => 1,
            _ => 0
        };
    }

    public static string EndpointName => nameof(GetPublishedStory);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("stories/{id:ulid}", async (
                [FromRoute] Ulid id,
                [AsParameters] GetPublishedStoryQuery query,
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

    private static string GetCurrentStoryType(Story story)
    {
        return story switch
        {
            { } when story.HasBooks => StoryType.MultiBook,
            { } when story.HasChapters => StoryType.MultiChapter,
            { } when story.HasContent => StoryType.SingleBody,
            { } when !story.HasContent => StoryType.New,
            _ => StoryType.Unknown
        };
    }
}
