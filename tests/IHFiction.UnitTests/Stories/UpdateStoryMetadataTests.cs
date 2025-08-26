using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Stories;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for UpdateStoryMetadata functionality
/// Tests validation logic and request handling without database dependencies
/// </summary>
public class UpdateStoryMetadataTests
{


    [Fact]
    public void UpdateStoryMetadataRequest_WithValidData_PassesValidation()
    {
        // Arrange
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: "Updated Story Title",
            Description: "This is an updated description with enough content to pass validation.");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithEmptyTitle_FailsValidation()
    {
        // Arrange
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: "",
            Description: "This is a valid description with enough content.");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("Title is required"));
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithNullTitle_FailsValidation()
    {
        // Arrange
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: null,
            Description: "This is a valid description with enough content.");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("Title is required"));
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithTooLongTitle_FailsValidation()
    {
        // Arrange
        var longTitle = new string('a', 201); // Exceeds 200 character limit
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: longTitle,
            Description: "This is a valid description with enough content.");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("must be between 1 and 200 characters"));
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithEmptyDescription_FailsValidation()
    {
        // Arrange
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: "Valid Title",
            Description: "");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("Description is required"));
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithTooShortDescription_FailsValidation()
    {
        // Arrange
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: "Valid Title",
            Description: "Short"); // Less than 10 characters

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("must be between 10 and 2000 characters"));
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithTooLongDescription_FailsValidation()
    {
        // Arrange
        var longDescription = new string('a', 2001); // Exceeds 2000 character limit
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: "Valid Title",
            Description: longDescription);

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("must be between 10 and 2000 characters"));
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithExcessiveWhitespaceInTitle_FailsValidation()
    {
        // Arrange
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: "Title   with   excessive   whitespace",
            Description: "This is a valid description with enough content.");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("excessive whitespace"));
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithExcessiveWhitespaceInDescription_FailsValidation()
    {
        // Arrange
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: "Valid Title",
            Description: "Description     with     excessive     whitespace     that     should     fail.");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("excessive whitespace"));
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithPotentiallyHarmfulContentInTitle_FailsValidation()
    {
        // Arrange
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: "Story <script>alert('xss')</script>",
            Description: "This is a valid description with enough content.");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("potentially harmful content"));
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithPotentiallyHarmfulContentInDescription_FailsValidation()
    {
        // Arrange
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: "Valid Title",
            Description: "This description contains <script>alert('xss')</script> harmful content.");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("potentially harmful content"));
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithMinimumValidContent_PassesValidation()
    {
        // Arrange
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: "A",
            Description: "1234567890"); // Exactly 10 characters

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithMaximumValidContent_PassesValidation()
    {
        // Arrange
        var maxTitle = new string('a', 200); // Exactly 200 characters
        var maxDescription = new string('b', 2000); // Exactly 2000 characters
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: maxTitle,
            Description: maxDescription);

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void UpdateStoryMetadataResponse_CanBeCreated()
    {
        // Arrange
        var id = Ulid.NewUlid();
        var ownerId = Ulid.NewUlid();
        var now = DateTime.UtcNow;

        // Act
        var response = new UpdateStoryMetadata.UpdateStoryMetadataResponse(
            id,
            "Updated Title",
            "Updated Description",
            now,
            ownerId,
            "Owner Name");

        // Assert
        Assert.Equal(id, response.Id);
        Assert.Equal("Updated Title", response.Title);
        Assert.Equal("Updated Description", response.Description);
        Assert.Equal(now, response.UpdatedAt);
        Assert.Equal(ownerId, response.OwnerId);
        Assert.Equal("Owner Name", response.OwnerName);
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithJavaScriptProtocol_FailsValidation()
    {
        // Arrange
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: "Valid Title",
            Description: "Click here: javascript:alert('xss') for more info.");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("potentially harmful content"));
    }

    [Fact]
    public void UpdateStoryMetadataRequest_WithEventHandlers_FailsValidation()
    {
        // Arrange
        var request = new UpdateStoryMetadata.UpdateStoryMetadataBody(
            Title: "Story onclick=alert('xss')",
            Description: "This is a valid description with enough content.");

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.ErrorMessage!.Contains("potentially harmful content"));
    }
}