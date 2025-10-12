using IHFiction.FictionApi.Extensions;
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
    public void AddChapterToStoryRequest_WithValidData_IsValid()
    {
        // Arrange
        var request = new CreateStoryChapter.CreateStoryChapterBody(
            Title: "Chapter 1: The Beginning",
            Content: "This is the chapter content.",
            Note1: "Author's note",
            Note2: "Additional note");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void AddChapterToStoryRequest_WithMinimalData_IsValid()
    {
        // Arrange
        var request = new CreateStoryChapter.CreateStoryChapterBody(
            Title: "Chapter Title",
            Content: "X"); // Minimum 1 character

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void AddChapterToStoryRequest_WithEmptyTitle_FailsValidation()
    {
        // Arrange
        var request = new CreateStoryChapter.CreateStoryChapterBody(
            Title: "",
            Content: "Valid content");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("Title is required"));
    }

    [Fact]
    public void AddChapterToStoryRequest_WithEmptyContent_FailsValidation()
    {
        // Arrange
        var request = new CreateStoryChapter.CreateStoryChapterBody(
            Title: "Valid Title",
            Content: "");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("Content is required"));
    }

    [Fact]
    public void AddChapterToStoryRequest_WithTooLongTitle_FailsValidation()
    {
        // Arrange
        var longTitle = new string('a', 201); // Exceeds 200 character limit
        var request = new CreateStoryChapter.CreateStoryChapterBody(
            Title: longTitle,
            Content: "Valid content");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("Title must be between 1 and 200 characters"));
    }

    [Fact]
    public void AddChapterToStoryRequest_WithHarmfulContentInTitle_FailsValidation()
    {
        // Arrange
        var request = new CreateStoryChapter.CreateStoryChapterBody(
            Title: "<script>alert('xss')</script>",
            Content: "Valid content");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("potentially harmful content"));
    }

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