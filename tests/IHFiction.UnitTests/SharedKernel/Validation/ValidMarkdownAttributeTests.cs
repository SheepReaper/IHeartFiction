using System.ComponentModel.DataAnnotations;

using IHFiction.SharedKernel.Markdown;


namespace IHFiction.UnitTests.SharedKernel.Validation;

public class ValidMarkdownAttributeTests
{
    private readonly ValidMarkdownAttribute _attribute = new();
    private readonly ValidationContext _context = new(new object());

    [Fact]
    public void IsValid_WithNullValue_ReturnsSuccess()
    {
        // Act
        var result = _attribute.GetValidationResult(null, _context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithEmptyString_ReturnsSuccess()
    {
        // Act
        var result = _attribute.GetValidationResult("", _context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithPlainText_ReturnsSuccess()
    {
        // Arrange
        var content = "This is just plain text without any markdown.";

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithBasicMarkdown_ReturnsSuccess()
    {
        // Arrange
        var content = """
            # Heading 1
            ## Heading 2
            
            This is **bold** and *italic* text.
            
            - List item 1
            - List item 2
            
            > This is a blockquote
            
            `inline code`
            
            ```
            code block
            ```
            """;

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithValidHttpsImage_ReturnsSuccess()
    {
        // Arrange
        var content = "![Alt text](https://imgur.com/image.png)";

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithValidBase64Image_ReturnsSuccess()
    {
        // Arrange
        var content = "![Alt text](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==)";

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithValidHttpsLink_ReturnsSuccess()
    {
        // Arrange
        var content = "[Link text](https://example.com)";

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }

    [Fact]
    public void IsValid_WithJavaScriptProtocol_ReturnsError()
    {
        // Arrange
        var content = "[Click me](javascript:alert('xss'))";

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("harmful", result?.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithDisallowedImageDomain_ReturnsError()
    {
        // Arrange
        var content = "![Alt text](https://evil-site.com/image.png)";

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("domain not allowed", result?.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithHttpImageInProduction_ReturnsError()
    {
        // Arrange
        var content = "![Alt text](http://imgur.com/image.png)";

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("HTTPS protocol", result?.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithOversizedBase64Image_ReturnsError()
    {
        // Arrange - Create a large base64 string (over 5MB)
        var largeBase64 = new string('A', 7000000); // ~7MB when decoded
        var content = $"![Alt text](data:image/png;base64,{largeBase64})";

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("exceeds maximum size", result?.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithInvalidBase64ImageFormat_ReturnsError()
    {
        // Arrange
        var content = "![Alt text](data:image/exe;base64,invalidbase64)";

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("Invalid base64 image format", result?.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithScriptTag_ReturnsError()
    {
        // Arrange
        var content = "This content has <script>alert('xss')</script> in it.";

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("harmful", result?.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithEventHandler_ReturnsError()
    {
        // Arrange
        var content = "This content has onclick=alert('xss') in it.";

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("harmful", result?.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithNonStringValue_ReturnsError()
    {
        // Act
        var result = _attribute.GetValidationResult(123, _context);

        // Assert
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("must be a string", result?.ErrorMessage);
    }

    [Fact]
    public void IsValid_WithComplexValidMarkdown_ReturnsSuccess()
    {
        // Arrange
        var content = """
            # My Story Chapter
            
            This is a story with **bold** and *italic* text.
            
            ![Character portrait](https://imgur.com/character.png "Character Name")
            
            The character said:
            
            > "This is a quote from the character."
            
            For more information, visit [the author's website](https://example.com).
            
            ## Technical Details
            
            Here's some code the character wrote:
            
            ```python
            def hello_world():
                print("Hello, World!")
            ```
            
            - Point 1
            - Point 2
              - Nested point
            
            1. Numbered item
            2. Another numbered item
            """;

        // Act
        var result = _attribute.GetValidationResult(content, _context);

        // Assert
        Assert.Equal(ValidationResult.Success, result);
    }
}
