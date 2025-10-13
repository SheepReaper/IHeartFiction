using System.ComponentModel.DataAnnotations;

using IHFiction.FictionApi.Account;
using IHFiction.FictionApi.Common;
using IHFiction.SharedKernel.Validation;

namespace IHFiction.UnitTests.Authors;

/// <summary>
/// Unit tests for UpdateAuthorProfile functionality.
/// These tests focus on validation, business logic, and entity tracking behavior.
/// </summary>
public class UpdateAuthorProfileTests
{
    [Fact]
    public void UpdateAuthorProfileRequest_Bio_HasCorrectValidationAttributes()
    {
        // Arrange
        var requestType = typeof(UpdateOwnAuthorProfile.UpdateOwnAuthorProfileBody);
        var bioProperty = requestType.GetProperty("Bio");

        // Act
        var attributes = bioProperty?.GetCustomAttributes(true);

        // Assert
        Assert.NotNull(bioProperty);
        Assert.NotNull(attributes);
        Assert.Contains(attributes, a => a is StringLengthAttribute);
        Assert.Contains(attributes, a => a is NoExcessiveWhitespaceAttribute);
        Assert.Contains(attributes, a => a is NoHarmfulContentAttribute);

        // Verify StringLength configuration
        var stringLengthAttr = attributes.OfType<StringLengthAttribute>().First();
        Assert.Equal(2000, stringLengthAttr.MaximumLength);
        Assert.Equal(10, stringLengthAttr.MinimumLength);

        // Verify NoExcessiveWhitespace configuration
        var whitespaceAttr = attributes.OfType<NoExcessiveWhitespaceAttribute>().First();
        Assert.Equal(5, whitespaceAttr.MaxConsecutiveWhitespace);
    }

    [Fact]
    public void UpdateAuthorProfileRequest_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var bio = "Test bio content";
        var request1 = new UpdateOwnAuthorProfile.UpdateOwnAuthorProfileBody(bio);
        var request2 = new UpdateOwnAuthorProfile.UpdateOwnAuthorProfileBody(bio);
        var request3 = new UpdateOwnAuthorProfile.UpdateOwnAuthorProfileBody("Different bio");

        // Act & Assert
        Assert.Equal(request1, request2);
        Assert.NotEqual(request1, request3);
        Assert.Equal(request1.GetHashCode(), request2.GetHashCode());
    }
}

/// <summary>
/// Mock tests for business logic scenarios that would be covered in integration tests.
/// These demonstrate the test patterns without requiring full database setup.
/// </summary>
public class UpdateAuthorProfileBusinessLogicTests
{
    [Fact]
    public void InputSanitizationService_SanitizeBio_NormalizesWhitespace()
    {
        // Arrange
        var bioWithExcessiveWhitespace = "This bio has     too many spaces    in a row.";

        // Act
        var sanitized = InputSanitizationService.SanitizeBio(bioWithExcessiveWhitespace);

        // Assert
        Assert.NotNull(sanitized);
        Assert.DoesNotContain("     ", sanitized); // Multiple spaces should be normalized
        Assert.Contains("This bio has too many spaces in a row.", sanitized);
    }

    [Fact]
    public void InputSanitizationService_SanitizeBio_PreservesMarkdown()
    {
        // Arrange
        var markdownBio = "I'm a writer who loves **bold text** and *italic text*.";

        // Act
        var sanitized = InputSanitizationService.SanitizeBio(markdownBio);

        // Assert
        Assert.Contains("**bold text**", sanitized);
        Assert.Contains("*italic text*", sanitized);
    }

    [Fact]
    public void InputSanitizationService_SanitizeBio_HandlesNullInput()
    {
        // Arrange
        string? nullBio = null;

        // Act
        var sanitized = InputSanitizationService.SanitizeBio(nullBio);

        // Assert
        Assert.Null(sanitized);
    }

    [Fact]
    public void InputSanitizationService_SanitizeBio_HandlesEmptyInput()
    {
        // Arrange
        var emptyBio = "";

        // Act
        var sanitized = InputSanitizationService.SanitizeBio(emptyBio);

        // Assert
        Assert.Null(sanitized); // SanitizeOptionalText returns null for empty input
    }

    [Fact]
    public void InputSanitizationService_SanitizeBio_HandlesWhitespaceOnlyInput()
    {
        // Arrange
        var whitespaceOnlyBio = "   \t\n   ";

        // Act
        var sanitized = InputSanitizationService.SanitizeBio(whitespaceOnlyBio);

        // Assert
        Assert.Null(sanitized); // SanitizeOptionalText returns null for whitespace-only input
    }

    [Fact]
    public void InputSanitizationService_SanitizeBio_TrimsAndNormalizesWhitespace()
    {
        // Arrange
        var bioWithWhitespace = "  This is a bio with   extra spaces  and  tabs\t\t  ";

        // Act
        var sanitized = InputSanitizationService.SanitizeBio(bioWithWhitespace);

        // Assert
        Assert.NotNull(sanitized);
        Assert.Equal("This is a bio with extra spaces and tabs", sanitized);
    }
}