#pragma warning disable CA1515 // Wolverine discovers public message types.

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
using Wolverine;

namespace IHFiction.FictionApi.Stories;

public sealed record TagCreatedRequested(Ulid TagId);

internal sealed class AddTagsToStory(
    FictionDbContext context,
    UserService userService,
    IMessageBus messageBus) : IUseCase, INameEndpoint<AddTagsToStory>
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
        public static readonly DomainError DuplicateTag = new("AddTagsToStory.DuplicateTag", "The request contains equivalent tag values.");
    }


    /// <summary>
    /// Request model for adding tags to a story.
    /// </summary>
    /// <param name="Tags">Complete set of tags that should be assigned to the story.</param>
    internal sealed record AddTagsToStoryBody(
        [property: Required(ErrorMessage = "Tags are required.")]
        [property: MaxLength(50, ErrorMessage = "A story can have at most 50 tags.")]
        IReadOnlyCollection<string> Tags
    );

    internal sealed record AddTagsToStoryQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<AddTagsToStoryResponse>]
        string Fields = ""
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
    /// <param name="Tags">Final canonical tag set assigned to the story.</param>
    /// <param name="TotalTags">Total number of tags associated with the story.</param>
    internal sealed record AddTagsToStoryResponse(
        Ulid StoryId,
        string StoryTitle,
        IReadOnlyList<AddedTagItem> Tags,
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

        ArgumentNullException.ThrowIfNull(body.Tags);

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
            var parsedTags = new List<(string Category, string? Subcategory, string Value, string NormalizedKey)>();
            var requestedKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var tagString in body.Tags)
            {
                var parseResult = ParseTag(tagString);
                if (parseResult.IsFailure)
                {
                    return parseResult.DomainError;
                }

                var (category, subcategory, value) = parseResult.Value;
                category = InputSanitizationService.SanitizeTag(category);
                subcategory = subcategory is null ? null : InputSanitizationService.SanitizeTag(subcategory);
                value = InputSanitizationService.SanitizeTag(value);
                var normalizedKey = Tag.BuildNormalizedKey(category, subcategory, value);

                if (!requestedKeys.Add(normalizedKey))
                {
                    return Errors.DuplicateTag;
                }

                parsedTags.Add((category, subcategory, value, normalizedKey));
            }

            var resolvedTags = new List<CanonicalTag>();
            var createdTagIds = new List<Ulid>();

            foreach (var (category, subcategory, value, normalizedKey) in parsedTags)
            {
                var tagMatch = await context.Tags
                    .FirstOrDefaultAsync(tag => tag.NormalizedKey == normalizedKey, cancellationToken);

                if (tagMatch is not null)
                {
                    if (tagMatch is SynonymTag synonym)
                    {
                        await context.Entry(synonym)
                            .Reference(tag => tag.CanonicalTag)
                            .LoadAsync(cancellationToken);
                    }

                    resolvedTags.Add((CanonicalTag)tagMatch.ResolveCanonical());
                }
                else
                {
                    var createdTag = Tag.CreateCanonical(category, subcategory, value);
                    context.Tags.Add(createdTag);
                    resolvedTags.Add(createdTag);
                    createdTagIds.Add(createdTag.Id);
                }
            }

            foreach (var tag in story.Tags.Where(tag => !resolvedTags.Any(resolved => resolved.Id == tag.ResolveCanonical().Id)).ToArray())
            {
                story.Tags.Remove(tag);
            }

            foreach (var tag in resolvedTags.Where(tag => !story.Tags.Any(existing => existing.ResolveCanonical().Id == tag.Id)))
            {
                story.Tags.Add(tag);
            }

            await context.SaveChangesAsync(cancellationToken);

            foreach (var createdTagId in createdTagIds)
            {
                await messageBus.PublishAsync(new TagCreatedRequested(createdTagId));
            }

            return new AddTagsToStoryResponse(
                story.Id,
                story.Title,
                [.. resolvedTags.Select(tag => new AddedTagItem(tag.Category, tag.Subcategory, tag.Value, createdTagIds.Contains(tag.Id)))],
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

        return parts.Length == 2
            ? (parts[0], null, parts[1])
            : (parts[0], parts[1], parts[2]);
    }
        public static string EndpointName => nameof(AddTagsToStory);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapPut("stories/{id:ulid}/tags", async (
                [FromRoute] Ulid id,
                [AsParameters] AddTagsToStoryQuery query,
                [FromBody] AddTagsToStoryBody body,
                AddTagsToStory useCase,
                LinkService linker,
                ClaimsPrincipal claimsPrincipal,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, body, claimsPrincipal, cancellationToken);

                return result
                    .WithLinks(linker, AddTagsToStory.EndpointName, method: HttpMethods.Put, values: [new KeyValuePair<string, string?>("id", id.ToString())])
                    .ToOkResult(query);
            })
            .WithSummary("Replace Story Tags")
            .WithDescription("Replaces a story's complete tag set for categorization and discovery. " +
                "Tags must be in the format 'category:value' or 'category:subcategory:value'. " +
                "Only story owners and authorized collaborators can replace tags. " +
                "An empty collection removes every tag. Equivalent duplicate values are rejected. " +
                "Requires authentication and appropriate permissions.")
            .WithTags(ApiTags.Stories.Management)
            .RequireAuthorization("author") // Authentication required
            .WithStandardResponses(conflict: false)
            .Produces<Linked<AddTagsToStoryResponse>>()
            .Accepts<AddTagsToStoryBody>(MediaTypeNames.Application.Json);
        }
    }
}
