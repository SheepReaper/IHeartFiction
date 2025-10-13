using IHFiction.FictionApi.Stories;

using MongoDB.Bson;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for AddChapterToStory functionality
/// Tests request validation and response model construction
/// </summary>
public class AddChapterToStoryTests
{
    [Fact]
    public void AddChapterToStoryResponse_CanBeCreated()
    {
        // Arrange
        var chapterId = Ulid.NewUlid();
        var contentId = ObjectId.GenerateNewId();
        var storyId = Ulid.NewUlid();
        var chapterCreatedAt = DateTime.UtcNow;
        var chapterUpdatedAt = DateTime.UtcNow;
        var storyUpdatedAt = DateTime.UtcNow.AddMinutes(1);
        var content = "This is the chapter content.";
        var note1 = "Author's note";
        var note2 = "Additional note";
        var contentUpdatedAt = DateTime.UtcNow;
        var contentPublishedAt = DateTime.UtcNow;

        // Act
        var response = new CreateStoryChapter.CreateStoryChapterResponse(
            storyId,
            "Test Story",
            storyUpdatedAt,
            chapterId,
            "Chapter 1: The Beginning",
            chapterCreatedAt,
            contentPublishedAt,
            chapterUpdatedAt,
            contentId,
            content,
            note1,
            note2,
            contentUpdatedAt
        );

        // Assert
        Assert.Equal(chapterId, response.ChapterId);
        Assert.Equal("Chapter 1: The Beginning", response.ChapterTitle);
        Assert.Equal(contentId, response.ContentId);
        Assert.Equal(storyId, response.StoryId);
        Assert.Equal("Test Story", response.StoryTitle);
        Assert.Equal(chapterCreatedAt, response.ChapterCreatedAt);
        Assert.Equal(storyUpdatedAt, response.StoryUpdatedAt);
    }


}