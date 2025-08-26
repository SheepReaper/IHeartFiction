using System.ComponentModel.DataAnnotations;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Stories;
using IHFiction.SharedKernel.Validation;

namespace IHFiction.UnitTests.Stories;

/// <summary>
/// Unit tests for CreateStory functionality.
/// These tests focus on configuration verification and basic integration testing.
/// Detailed validation logic is tested in ValidationAttributeTests.
/// </summary>
public class CreateStoryTests
{
    [Fact]
    public void CreateStoryRequest_WithValidData_PassesValidation()
    {
        // Arrange
        var request = new CreateStory.CreateStoryBody(
            Title: "My Amazing Story",
            Description: "This is a compelling story about adventure and discovery that will captivate readers."
        );

        // Act
        var isValid = request.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }



    [Fact]
    public void CreateStoryRequest_Title_HasCorrectValidationAttributes()
    {
        // Arrange
        var titleProperty = typeof(CreateStory.CreateStoryBody)
            .GetProperty(nameof(CreateStory.CreateStoryBody.Title));

        // Act
        var attributes = titleProperty?.GetCustomAttributes(true);

        // Assert
        Assert.NotNull(attributes);
        Assert.Contains(attributes, a => a is RequiredAttribute);
        Assert.Contains(attributes, a => a is StringLengthAttribute);
        Assert.Contains(attributes, a => a is NoExcessiveWhitespaceAttribute);
        Assert.Contains(attributes, a => a is NoHarmfulContentAttribute);

        // Verify StringLength configuration
        var stringLengthAttr = attributes.OfType<StringLengthAttribute>().First();
        Assert.Equal(200, stringLengthAttr.MaximumLength);
        Assert.Equal(1, stringLengthAttr.MinimumLength);

        // Verify NoExcessiveWhitespace configuration
        var whitespaceAttr = attributes.OfType<NoExcessiveWhitespaceAttribute>().First();
        Assert.Equal(3, whitespaceAttr.MaxConsecutiveWhitespace);
    }

    [Fact]
    public void CreateStoryRequest_Description_HasCorrectValidationAttributes()
    {
        // Arrange
        var descriptionProperty = typeof(CreateStory.CreateStoryBody)
            .GetProperty(nameof(CreateStory.CreateStoryBody.Description));

        // Act
        var attributes = descriptionProperty?.GetCustomAttributes(true);

        // Assert
        Assert.NotNull(attributes);
        Assert.Contains(attributes, a => a is RequiredAttribute);
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


}