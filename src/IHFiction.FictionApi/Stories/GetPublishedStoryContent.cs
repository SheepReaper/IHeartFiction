using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Linking;

using MongoDB.Bson;
using MongoDB.Driver;

namespace IHFiction.FictionApi.Stories;

internal sealed class GetPublishedStoryContent(
    IMongoCollection<WorkBody> workBodies,
    EntityLoaderService entityLoader) : IUseCase, INameEndpoint<GetPublishedStoryContent>
{
    internal sealed record GetPublishedStoryContentQuery(
        [property: StringLength(50, ErrorMessage = "Fields must be 50 characters or less.")]
        [property: ShapesType<GetPublishedStoryContentResponse>]
        string Fields = ""
    ) : IDataShapingSupport;

    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly DomainError StoryNotFound = CommonErrors.Story.NotFound;
        public static readonly DomainError DatabaseError = CommonErrors.Database.ConnectionFailed;

        // Business logic errors specific to content retrieval
        public static readonly DomainError InvalidStoryStructure = new("GetStoryContent.InvalidStoryStructure", "Story has chapters or books and cannot have direct content.");
        public static readonly DomainError ContentNotFound = new("GetStoryContent.ContentNotFound", "Story content not found.");
        public static readonly DomainError NoContent = new("GetStoryContent.NoContent", "Story does not have content yet.");
        public static readonly DomainError StoryNotPublished = new("GetStoryContent.StoryNotPublished", "Story is not published and cannot be accessed.");
    }

    /// <summary>
    /// Response model for retrieving story content.
    /// </summary>
    /// <param name="StoryId">Unique identifier of the story</param>
    /// <param name="StoryTitle">Title of the story</param>
    /// <param name="StoryDescription">Description of the story</param>
    /// <param name="ContentId">Unique identifier for the content document</param>
    /// <param name="Content">The story content in markdown format</param>
    /// <param name="Note1">Optional author note about the content</param>
    /// <param name="Note2">Optional second author note</param>
    /// <param name="ContentUpdatedAt">When the content was last updated</param>
    /// <param name="StoryUpdatedAt">When the story metadata was last updated</param>
    internal sealed record GetPublishedStoryContentResponse(
        Ulid StoryId,
        string StoryTitle,
        string StoryDescription,
        ObjectId ContentId,
        string Content,
        string? Note1,
        string? Note2,
        DateTime ContentUpdatedAt,
        DateTime StoryUpdatedAt);

    public async Task<Result<GetPublishedStoryContentResponse>> HandleAsync(
        Ulid id,
        CancellationToken cancellationToken = default)
    {
        // Load the story with full details using the centralized entity loader
        var story = await entityLoader.LoadStoryWithFullDetailsAsync(id, asNoTracking: true, cancellationToken: cancellationToken);

        if (story is null)
            return Errors.StoryNotFound;

        // Only allow access to published stories
        if (!story.IsPublished)
            return Errors.StoryNotPublished;

        // Validate story structure - stories with chapters or books cannot have direct content
        if (story.HasChapters || story.HasBooks)
            return Errors.InvalidStoryStructure;

        // Check if story has content
        if (!story.HasContent)
            return Errors.NoContent;

        // Get the content from MongoDB
        var workBody = await workBodies.Find(wb => wb.Id == story.WorkBodyId).FirstOrDefaultAsync(cancellationToken);

        return workBody is null
            ? Errors.ContentNotFound
            : new GetPublishedStoryContentResponse(
            story.Id,
            story.Title,
            story.Description,
            workBody.Id,
            workBody.Content,
            workBody.Note1,
            workBody.Note2,
            workBody.UpdatedAt,
            story.UpdatedAt);
    }
    public static string EndpointName => nameof(GetPublishedStoryContent);

    internal sealed class Endpoint : IEndpoint
    {
        public string Name => EndpointName;

        public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder builder)
        {
            return builder.MapGet("stories/{id:ulid}/content", async (
                [FromRoute] Ulid id,
                [AsParameters] GetPublishedStoryContentQuery query,
                GetPublishedStoryContent useCase,
                CancellationToken cancellationToken) =>
            {
                var result = await useCase.HandleAsync(id, cancellationToken);

                return result.ToOkResult(query);
            })
            .WithSummary("Get Published Story Content")
            .WithDescription("Retrieves the content of a published story that has direct content (not chapters or books). " +
                "This is a public endpoint that allows anyone to read published story content. " +
                "Returns the markdown content along with any author notes and metadata. " +
                "No authentication required.")
            .WithTags(ApiTags.Stories.Discovery)
            .AllowAnonymous() // Public endpoint - no authentication required
            .WithStandardResponses(unauthorized: false, conflict: false, validation: false)
            .Produces<Linked<GetPublishedStoryContentResponse>>();
        }
    }
}
