using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Features.Reading;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

using MongoDB.Bson;

namespace IHFiction.UnitTests.Features.Reading;

/// <summary>
/// Unit tests for GetStoryContent functionality
/// Tests response model construction and error codes
/// </summary>
public class GetStoryContentTests
{
    [Fact]
    public void GetStoryContentResponse_CanBeCreated()
    {
        // Arrange
        var storyId = Ulid.NewUlid();
        var contentId = ObjectId.GenerateNewId();
        var contentUpdatedAt = DateTime.UtcNow;
        var storyUpdatedAt = DateTime.UtcNow.AddMinutes(1);

        // Act
        var response = new GetPublishedStoryContent.GetPublishedStoryContentResponse(
            storyId,
            "Test Story",
            "A test story description",
            contentId,
            "This is the story content with **markdown** formatting.",
            "Author's note",
            "Additional note",
            contentUpdatedAt,
            storyUpdatedAt);

        // Assert
        Assert.Equal(storyId, response.StoryId);
        Assert.Equal("Test Story", response.StoryTitle);
        Assert.Equal(contentId, response.ContentId);
        Assert.Equal("This is the story content with **markdown** formatting.", response.Content);
        Assert.Equal("Author's note", response.Note1);
        Assert.Equal("Additional note", response.Note2);
        Assert.Equal(contentUpdatedAt, response.ContentUpdatedAt);
        Assert.Equal(storyUpdatedAt, response.StoryUpdatedAt);
    }

    [Fact]
    public async Task HandleAsync_ContentFreePublishedStory_ReturnsNoContentMappedTo404NotFound()
    {
        var options = new DbContextOptionsBuilder<FictionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new FictionDbContext(options);

        var author = new Author { Id = Ulid.NewUlid(), UserId = Guid.NewGuid(), Name = "Author" };
        var story = new Story
        {
            Id = Ulid.NewUlid(),
            Title = "Multi-Chapter Story Without Direct Body",
            Description = "Chapters hold the prose.",
            Owner = author,
            OwnerId = author.Id,
            PublishedAt = DateTime.UtcNow,
            WorkBodyId = null
        };
        context.AddRange(author, story);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new GetPublishedStoryContent(workBodies: null!, new EntityLoaderService(context));
        var result = await useCase.HandleAsync(story.Id, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("GetStoryContent.NoContent", result.DomainError.Code);
        Assert.Contains("/stories/{id}/chapters", result.DomainError.Description);

        var httpResult = result.ToOkResult(new GetPublishedStoryContent.GetPublishedStoryContentQuery());
        var problemResult = Assert.IsType<ProblemHttpResult>(httpResult);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
        Assert.True(problemResult.ProblemDetails.Extensions.ContainsKey("domainError"));
    }
}
