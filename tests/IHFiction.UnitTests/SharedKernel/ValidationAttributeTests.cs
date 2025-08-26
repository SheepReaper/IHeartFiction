using System.ComponentModel.DataAnnotations;

using IHFiction.SharedKernel.Validation;

namespace IHFiction.UnitTests.SharedKernel;

/// <summary>
/// Comprehensive unit tests for custom validation attributes in SharedKernel.
/// These tests cover all edge cases and validation scenarios for the attributes themselves.
/// </summary>
public class ValidationAttributeTests
{
    #region NoExcessiveWhitespaceAttribute Tests

    [Theory]
    [InlineData("Normal text", true)]
    [InlineData("Text with  two spaces", true)]
    [InlineData("Text with   three spaces", false)]
    [InlineData("Text with    four spaces", false)]
    [InlineData("Multiple   groups   of   spaces", false)]
    [InlineData("", true)]
    [InlineData("   ", true)] // Only whitespace should pass (gets trimmed)
    [InlineData("Start   with spaces", false)]
    [InlineData("End with   spaces", false)] // Fixed: internal spaces matter, not trailing
    public void NoExcessiveWhitespaceAttribute_WithThreshold3_ValidatesCorrectly(string input, bool shouldPass)
    {
        // Arrange
        var attribute = new NoExcessiveWhitespaceAttribute(3);
        var context = new ValidationContext(new object()) { MemberName = "TestProperty" };

        // Act
        var result = attribute.GetValidationResult(input, context);

        // Assert
        if (shouldPass)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("excessive whitespace", result?.ErrorMessage);
        }
    }

    [Theory]
    [InlineData("Text with    four spaces", true)]
    [InlineData("Text with     five spaces", false)]
    [InlineData("Text with      six spaces", false)]
    public void NoExcessiveWhitespaceAttribute_WithThreshold5_ValidatesCorrectly(string input, bool shouldPass)
    {
        // Arrange
        var attribute = new NoExcessiveWhitespaceAttribute(5);
        var context = new ValidationContext(new object()) { MemberName = "TestProperty" };

        // Act
        var result = attribute.GetValidationResult(input, context);

        // Assert
        if (shouldPass)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("excessive whitespace", result?.ErrorMessage);
        }
    }

    [Fact]
    public void NoExcessiveWhitespaceAttribute_WithNullValue_PassesValidation()
    {
        // Arrange
        var attribute = new NoExcessiveWhitespaceAttribute(3);
        var context = new ValidationContext(new object()) { MemberName = "TestProperty" };

        // Act
        var result = attribute.GetValidationResult(null, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void NoExcessiveWhitespaceAttribute_WithNonStringValue_FailsValidation()
    {
        // Arrange
        var attribute = new NoExcessiveWhitespaceAttribute(3);
        var context = new ValidationContext(new object()) { MemberName = "TestProperty" };

        // Act
        var result = attribute.GetValidationResult(123, context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("must be a string", result?.ErrorMessage);
    }

    #endregion

    #region NoHarmfulContentAttribute Tests

    [Theory]
    [InlineData("This is safe content", true)]
    [InlineData("Normal text with <b>bold</b> tags", true)]
    [InlineData("Text with numbers 123 and symbols !@#", true)]
    [InlineData("<script>alert('xss')</script>", false)]
    [InlineData("<SCRIPT>alert('xss')</SCRIPT>", false)] // Case insensitive
    [InlineData("javascript:void(0)", false)]
    [InlineData("JAVASCRIPT:alert('xss')", false)] // Case insensitive
    [InlineData("onclick=alert('xss')", false)]
    [InlineData("ONCLICK=alert('xss')", false)] // Case insensitive
    [InlineData("onmouseover=alert('xss')", false)]
    [InlineData("Text with <script> in middle", false)]
    [InlineData("Text with javascript: protocol", false)]
    [InlineData("Text with onclick= handler", false)]
    [InlineData("", true)]
    [InlineData("   ", true)] // Only whitespace
    public void NoHarmfulContentAttribute_ValidatesCorrectly(string input, bool shouldPass)
    {
        // Arrange
        var attribute = new NoHarmfulContentAttribute();
        var context = new ValidationContext(new object()) { MemberName = "TestProperty" };

        // Act
        var result = attribute.GetValidationResult(input, context);

        // Assert
        if (shouldPass)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
            Assert.Contains("harmful content", result?.ErrorMessage);
        }
    }

    [Fact]
    public void NoHarmfulContentAttribute_WithNullValue_PassesValidation()
    {
        // Arrange
        var attribute = new NoHarmfulContentAttribute();
        var context = new ValidationContext(new object()) { MemberName = "TestProperty" };

        // Act
        var result = attribute.GetValidationResult(null, context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void NoHarmfulContentAttribute_WithNonStringValue_FailsValidation()
    {
        // Arrange
        var attribute = new NoHarmfulContentAttribute();
        var context = new ValidationContext(new object()) { MemberName = "TestProperty" };

        // Act
        var result = attribute.GetValidationResult(123, context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("must be a string", result?.ErrorMessage);
    }

    #endregion


}
