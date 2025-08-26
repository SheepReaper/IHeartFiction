using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Stories;

using MongoDB.Bson;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for UpdateChapterContent functionality
/// Tests request validation and response model construction
/// </summary>
public class UpdateChapterContentTests
{
    [Fact]
    public void UpdateChapterContentRequest_WithValidContent_IsValid()
    {
        // Arrange
        var request = new UpdateChapterContent.UpdateChapterContentBody(
            Content: "This is valid chapter content.",
            Note1: "Author's note",
            Note2: "Additional note");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void UpdateChapterContentRequest_WithMinimalContent_IsValid()
    {
        // Arrange
        var request = new UpdateChapterContent.UpdateChapterContentBody(
            Content: "X"); // Minimum 1 character

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void UpdateChapterContentRequest_WithEmptyContent_FailsValidation()
    {
        // Arrange
        var request = new UpdateChapterContent.UpdateChapterContentBody(
            Content: "");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("Content is required"));
    }

    [Fact]
    public void UpdateChapterContentRequest_WithHarmfulContentInContent_FailsValidation()
    {
        // Arrange
        var request = new UpdateChapterContent.UpdateChapterContentBody(
            Content: "<script>alert('xss')</script>");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("potentially harmful content"));
    }
}