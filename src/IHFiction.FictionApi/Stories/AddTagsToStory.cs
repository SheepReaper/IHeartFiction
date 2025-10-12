using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

namespace IHFiction.FictionApi.Stories;

internal sealed class AddTagsToStory(
    FictionDbContext context,
    UserService userService) : IUseCase, INameEndpoint<AddTagsToStory>
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly DomainError DatabaseError = CommonErrors.Database.ConnectionFailed;
        public static readonly DomainError AuthorNotFound = CommonErrors.Author.NotRegistered;

        // Business logic errors specific to adding tags to story
        public static readonly DomainError StoryNotFound = new("AddTagsToStory.StoryNotFound", "Story not found.");
        public static readonly DomainError AccessDenied = new("AddTagsToStory.AccessDenied", "You do not have permission to add tags to this story.");
        public static readonly DomainError NoTagsProvided = new("AddTagsToStory.NoTagsProvided", "At least one tag must be provided.");
        public static readonly DomainError InvalidTagFormat = new("AddTagsToStory.InvalidTagFormat", "Tag format is invalid. Expected format: 'category:value' or 'category:subcategory:value'.");
        public static readonly DomainError TagTooLong = new("AddTagsToStory.TagTooLong", "Tag components must be 50 characters or less.");
        public static readonly DomainError TagNotFound = new("AddTagsToStory.TagNotFound", "One or more tags do not exist. Only existing tags can be added to stories.");
    }


    /// <summary>
    /// Request model for adding tags to a story.
    /// </summary>
    /// <param name="Tags">List of tag strings to add to the story</param>
    internal sealed record AddTagsToStoryBody(
        [property: Required(ErrorMessage = "Tags are required.")]
        [property: MinLength(1, ErrorMessage = "At least one tag must be provided.")]
        string Tags
    );

    internal sealed record AddTagsToStoryQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<AddTagsToStoryResponse>]
        string? Fields = null
    ) : IDataShapingSupport;

    /// <summary>
    /// Represents a tag item that was added to the story.
    /// </summary>
    /// <param name="Category">The category of the tag</param>
    /// <param name="Subcategory">Optional subcategory of the tag</param>
    /// <param name="Value">The value of the tag</param>
    /// <param name="IsNew">Whether this tag was newly created</param>
    internal sealed record AddedTagItem(
        string Category,
        string? Subcategory,
        string Value,
        bool IsNew);

    /// <summary>
    /// Response model for adding tags to a story.
    /// </summary>
    /// <param name="StoryId">Unique identifier of the story</param>
    /// <param name="StoryTitle">Title of the story</param>
    /// <param name="AddedTags">List of tags that were successfully added</param>
    /// <param name="SkippedTags">List of tag strings that were skipped (already existed)</param>
    /// <param name="TotalTags">Total number of tags now associated with the story</param>
    internal sealed record AddTagsToStoryResponse(
        Ulid StoryId,
        string StoryTitle,
        IReadOnlyList<AddedTagItem> AddedTags,
        IReadOnlyList<string> SkippedTags,
        int TotalTags);

    public async Task<Result<AddTagsToStoryResponse>> HandleAsync(
        Ulid id,
        AddTagsToStoryBody body,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken cancellationToken = default)
    {
        // Get the current author
        var authorResult = await userService.GetAuthorAsync(claimsPrincipal, cancellationToken);
        if (authorResult.IsFailure) return Errors.AuthorNotFound;

        var currentUser = authorResult.Value;

        string[] tags = [.. body.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        // Validate tags are provided
        if (tags.Length == 0)
            return Errors.NoTagsProvided;

        try
        {
            // Get the story with necessary includes
            var story = await context.Stories
                .Include(s => s.Owner)
                .Include(s => s.Authors)
                .Include(s => s.Tags)
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (story is null)
                return Errors.StoryNotFound;

            // Check authorization - only owners and collaborators can add tags
            var isOwner = story.OwnerId == currentUser.Id;
            var isCollaborator = story.Authors.Any(a => a.Id == currentUser.Id);

            if (!isOwner && !isCollaborator)
                return Errors.AccessDenied;

            // Parse and validate tags
            var parsedTags = new List<(string Category, string? Subcategory, string Value)>();
            var skippedTags = new List<string>();

            foreach (var tagString in tags)
            {
                var parseResult = ParseTag(tagString);
                if (parseResult.IsFailure)
                {
                    skippedTags.Add(tagString);
                    continue;
                }

                parsedTags.Add(parseResult.Value);
            }

            // Get existing tags for the story to avoid duplicates
            var existingTagStrings = story.Tags.Select(t => t.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Find or create canonical tags
            var addedTags = new List<AddedTagItem>();
            var tagsToAdd = new List<Tag>();

            foreach (var (category, subcategory, value) in parsedTags)
            {
                var tagString = subcategory is null ? $"{category}:{value}" : $"{category}:{subcategory}:{value}";

                // Skip if tag already exists on story
                if (existingTagStrings.Contains(tagString))
                {
                    skippedTags.Add(tagString);
                    continue;
                }

                // Find existing tag or create new one
                var existingTag = await context.Tags
                    .FirstOrDefaultAsync(t =>
                        t.Category == category &&
                        t.Subcategory == subcategory &&
                        t.Value == value,
                        cancellationToken);

                if (existingTag is not null)
                {
                    tagsToAdd.Add(existingTag);
                    addedTags.Add(new AddedTagItem(category, subcategory, value, false));
                }
                else
                {
                    // Tag doesn't exist - skip it for now
                    // In a full implementation, this would create new canonical tags
                    skippedTags.Add(tagString);
                    continue;
                }
            }

            // Add tags to story
            foreach (var tag in tagsToAdd)
            {
                story.Tags.Add(tag);
            }

            await context.SaveChangesAsync(cancellationToken);

            return new AddTagsToStoryResponse(
                story.Id,
                story.Title,
                addedTags,
                skippedTags,
                story.Tags.Count);
        }
        catch (InvalidOperationException)
        {
            return Errors.DatabaseError;
        }
        catch (DbUpdateException)
        {
            return Errors.DatabaseError;
        }
    }

    private static Result<(string Category, string? Subcategory, string Value)> ParseTag(string tagString)
    {
        if (string.IsNullOrWhiteSpace(tagString))
            return Errors.InvalidTagFormat;

        var parts = tagString.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length < 2 || parts.Length > 3)
            return Errors.InvalidTagFormat;

        // Validate component lengths
        foreach (var part in parts)
        {
            if (part.Length > 50)
                return Errors.TagTooLong;
        }

        if (parts.Length == 2)
        {
            // Format: category:value
            return (parts[0], null, parts[1]);
        }
        else
        {
            // Format: category:subcategory:value
            return (parts[0], parts[1], parts[2]);
        }
    }
        public static string EndpointName => nameof(AddTagsToStory);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPost("stories/{id:ulid}/tags", async (
                [FromRoute] Ulid id,
                [AsParameters] AddTagsToStoryQuery query,
                [FromBody] AddTagsToStoryBody body,
                AddTagsToStory useCase,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, body, claimsPrincipal, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Add Tags to Story")
            .WithDescription("Adds one or more tags to a story for categorization and discovery. " +
                "Tags must be in the format 'category:value' or 'category:subcategory:value'. " +
                "Only story owners and authorized collaborators can add tags. " +
                "Existing tags are skipped, and only new tags are added. " +
                "Requires authentication and appropriate permissions.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author") // Authentication required
            .WithStandardResponses(conflict: false)
            .Produces<Linked<AddTagsToStoryResponse>>(StatusCodes.Status201Created)
            .Accepts<AddTagsToStoryBody>(MediaTypeNames.Application.Json);
        }
    }
}
