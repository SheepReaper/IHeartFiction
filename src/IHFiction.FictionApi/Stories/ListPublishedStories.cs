using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Pagination;
using IHFiction.SharedKernel.Searching;
using IHFiction.SharedKernel.Sorting;

namespace IHFiction.FictionApi.Stories;

internal sealed class ListPublishedStories(
    FictionDbContext context,
    IPaginationService paginator) : IUseCase, INameEndpoint<ListPublishedStories>
{
    internal static class Errors
    {
        public static readonly DomainError InvalidSort = new(
            "ListPublishedStories.InvalidSort",
            "Sort must use one or more of: publishedAt, title, updatedAt. Sort direction must be asc or desc.");

        public static readonly DomainError InvalidTagKey = new(
            "ListPublishedStories.InvalidTagKey",
            "TagKey must use the normalized category:value or category:subcategory:value format returned by GET /tags.");

        public static DomainError InvalidFields(string detail) => new(
            "ListPublishedStories.InvalidFields",
            $"Fields must be a comma-separated list of: storyId, title, description, publishedAt, updatedAt, hasContent, hasChapters, hasBooks, hasCoverImage, chapterCount, authorId, authorName, readCount, completionStatus. {detail}");
    }

    /// <param name="Page">One-based page number.</param>
    /// <param name="PageSize">Maximum number of stories returned per page.</param>
    /// <param name="Search">Optional title, description, or author-name search term.</param>
    /// <param name="Sort">Comma-separated sort terms using publishedAt, title, or updatedAt, each optionally followed by asc or desc.</param>
    /// <param name="Fields">Optional comma-separated story item fields. The paginated envelope and links are always retained.</param>
    /// <param name="AuthorId">Optional author identifier.</param>
    /// <param name="CompletionStatus">Optional InProgress or Complete status.</param>
    /// <param name="TagKey">Optional normalized canonical or synonym tag key returned by GET /tags.</param>
    internal sealed record ListPublishedStoriesQuery(
        [property: Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        int Page = 1,

        [property: Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
        int PageSize = 50,

        [property: FromQuery(Name = "q")]
        [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Search term must be between 2 and 100 characters.")]
        string Search = "",

        [property: StringLength(50, ErrorMessage = "Sort field must be 50 characters or less.")]
        string Sort = "publishedAt",

        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<ListPublishedStoriesItem>]
        string Fields = "",

        Ulid? AuthorId = null,

        [property: RegularExpression("^(InProgress|Complete)$", ErrorMessage = "Completion status must be InProgress or Complete.")]
        string? CompletionStatus = null,

        [property: StringLength(152, ErrorMessage = "Tag key must be 152 characters or less.")]
        string? TagKey = null
    ) : IPaginationSupport, ISearchSupport, ISortingSupport, IDataShapingSupport;

    private static readonly SortMapping[] SortMappings = [
        new(nameof(Story.PublishedAt)),
        new(nameof(Story.Title)),
        new(nameof(Story.UpdatedAt))];

    /// <summary>
    /// Represents a single published story item in the stories list response.
    /// </summary>
    /// <param name="StoryId">Unique identifier for the story</param>
    /// <param name="Title">Title of the story</param>
    /// <param name="Description">Description of the story</param>
    /// <param name="PublishedAt">When the story was published</param>
    /// <param name="UpdatedAt">When the story was last updated</param>
    /// <param name="HasContent">Whether the story has direct content</param>
    /// <param name="HasChapters">Whether the story has chapters</param>
    /// <param name="HasBooks">Whether the story has books</param>
    /// <param name="HasCoverImage">Whether the story has a cover image</param>
    /// <param name="ChapterCount">Number of chapters in the story</param>
    /// <param name="AuthorId">Unique identifier for the story author</param>
    /// <param name="AuthorName">Name of the story author</param>
    /// <param name="ReadCount">Qualified unique readers</param>
    /// <param name="CompletionStatus">Whether the story is in progress or complete</param>
    internal sealed record ListPublishedStoriesItem(
        Ulid StoryId,
        string Title,
        string Description,
        DateTime PublishedAt,
        DateTime UpdatedAt,
        bool HasContent,
        bool HasChapters,
        bool HasBooks,
        bool HasCoverImage,
        int ChapterCount,
        Ulid AuthorId,
        string AuthorName,
        int ReadCount,
        string CompletionStatus)
    {
        public ListPublishedStoriesItem(Ulid storyId, string title, string description, DateTime publishedAt,
            DateTime updatedAt, bool hasContent, bool hasChapters, bool hasBooks, bool hasCoverImage,
            int chapterCount, Ulid authorId, string authorName)
            : this(storyId, title, description, publishedAt, updatedAt, hasContent, hasChapters, hasBooks,
                hasCoverImage, chapterCount, authorId, authorName, 0, StoryCompletionStatus.InProgress.ToString()) { }
    }

    public async Task<Result<PagedCollection<ListPublishedStoriesItem>>> HandleAsync(
        ListPublishedStoriesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!SortMappings.Validate(query)) return Errors.InvalidSort;
        if (!DataShapingService.TryValidate<ListPublishedStoriesItem>(query.Fields, out var fieldsError))
            return Errors.InvalidFields(fieldsError.ErrorMessage ?? string.Empty);

        // Build the base query for published stories
        var completionStatus = string.IsNullOrWhiteSpace(query.CompletionStatus)
            ? (StoryCompletionStatus?)null
            : Enum.Parse<StoryCompletionStatus>(query.CompletionStatus, ignoreCase: false);

        var stories = context.Stories
            .AsNoTracking()
            .Where(s => s.PublishedAt != null)
            .Where(s => query.AuthorId == null || s.Authors.Any(a => a.Id == query.AuthorId))
            .Where(s => completionStatus == null || s.CompletionStatus == completionStatus);

        if (!TryNormalizeTagKey(query.TagKey, out var tagKey)) return Errors.InvalidTagKey;
        if (!string.IsNullOrWhiteSpace(tagKey))
        {
            var canonicalTagId = await context.Tags
                .AsNoTracking()
                .OfType<CanonicalTag>()
                .Where(tag => tag.NormalizedKey == tagKey)
                .Select(tag => (Ulid?)tag.Id)
                .Concat(context.Tags
                    .AsNoTracking()
                    .OfType<SynonymTag>()
                    .Where(tag => tag.NormalizedKey == tagKey)
                    .Select(tag => (Ulid?)tag.CanonicalTagId))
                .SingleOrDefaultAsync(cancellationToken);

            stories = canonicalTagId is null
                ? stories.Where(_ => false)
                : stories.Where(story => story.Tags.Any(tag => tag.Id == canonicalTagId.Value));
        }

        // Apply search filter if provided
        stories = stories.SearchIContains(query, s => s.Title, s => s.Description, s => s.Owner.Name);

        // Apply sorting
        stories = stories.ApplySort(query, SortMappings);

        // Apply projection
        var proj = stories.Select(s => new ListPublishedStoriesItem(
            s.Id,
            s.Title,
            s.Description,
            s.PublishedAt!.Value, // Safe because we filtered for non-null
            s.UpdatedAt,
            s.WorkBodyId != default,
            s.Chapters.Any(),
            s.Books.Any(),
            context.StoryCovers.Any(cover => cover.StoryId == s.Id),
            s.Chapters.Count,
            s.OwnerId,
            s.Owner.Name,
            s.ReadCount,
            s.CompletionStatus == StoryCompletionStatus.Complete ? "Complete" : "InProgress"));

        // Execute paginated query using the centralized service
        return await paginator.ExecutePagedQueryAsync(proj, query, cancellationToken);
    }

    private static bool TryNormalizeTagKey(string? tagKey, out string? normalizedTagKey)
    {
        normalizedTagKey = null;
        if (string.IsNullOrWhiteSpace(tagKey)) return true;

        var components = tagKey.Split(':', StringSplitOptions.TrimEntries);
        if (components.Length is < 2 or > 3 || components.Any(string.IsNullOrWhiteSpace)) return false;

        normalizedTagKey = components.Length switch
        {
            2 => Tag.BuildNormalizedKey(components[0], null, components[1]),
            3 => Tag.BuildNormalizedKey(components[0], components[1], components[2]),
            _ => null
        };
        return true;
    }

    public static string EndpointName => nameof(ListPublishedStories);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("stories/published", async (
                [AsParameters] ListPublishedStoriesQuery query,
                ListPublishedStories useCase,
                LinkService linker,
                CancellationToken cancellationToken) =>
            {
                var result = (await useCase
                    .HandleAsync(query, cancellationToken))
                    .WithLinks(
                        linker,
                        Name,
                        story => new(story, new List<LinkItem>() {
                            linker.Create<GetPublishedStory>("self", HttpMethods.Get, new[] { new KeyValuePair<string, string?>("id", story.StoryId.ToString()) })
                        }),
                        query,
                        routeValues:
                        [
                            new(nameof(ListPublishedStoriesQuery.AuthorId), query.AuthorId?.ToString()),
                            new(nameof(ListPublishedStoriesQuery.CompletionStatus), query.CompletionStatus),
                            new(nameof(ListPublishedStoriesQuery.Fields), query.Fields),
                            new(nameof(ListPublishedStoriesQuery.TagKey), query.TagKey)
                        ]);

                return result.ToOkResult(query);
            }
                )
                .WithSummary("List Published Stories")
                .WithDescription("Retrieves a paginated list of all publicly published stories. " +
                "Supports searching by title, description, or author name. " +
                "Results can be filtered by completion status or by a normalized tag key returned from GET /tags. " +
                "Canonical and synonym tag keys resolve to the same canonical tag family. " +
                "Sort accepts a comma-separated list of field direction pairs. " +
                "Valid sort fields: publishedAt, title, updatedAt. Valid directions: asc, desc (default asc). " +
                "Fields accepts a comma-separated list of response properties to include per item: " +
                "storyId, title, description, publishedAt, updatedAt, hasContent, hasChapters, hasBooks, " +
                "hasCoverImage, chapterCount, authorId, authorName, readCount, completionStatus. " +
                "Invalid Sort or Fields values return HTTP 400 with a domainError code and guidance. " +
                "This is a public endpoint that does not require authentication and only " +
                "returns stories that have been explicitly published by their authors.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous() // Public endpoint - no authentication required
            .WithStandardResponses(notFound: false, conflict: false, unauthorized: false, forbidden: false)
            .Produces<LinkedPagedCollection<ListPublishedStoriesItem>>();
        }
    }
}
