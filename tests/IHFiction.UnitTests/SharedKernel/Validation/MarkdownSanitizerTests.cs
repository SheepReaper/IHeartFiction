using IHFiction.SharedKernel.Markdown;

namespace IHFiction.UnitTests.SharedKernel.Validation;

public class MarkdownSanitizerTests
{
    private readonly MarkdownOptions _defaultOptions = new();
    private readonly MarkdownOptions _developmentOptions = new() { AllowInsecureHttp = true };

    [Fact]
    public void SanitizeContent_WithNullOrEmpty_ReturnsEmpty()
    {
        // Act & Assert
        Assert.Equal(string.Empty, MarkdownSanitizer.SanitizeContent(null!, _defaultOptions, false));
        Assert.Equal(string.Empty, MarkdownSanitizer.SanitizeContent("", _defaultOptions, false));
        Assert.Equal(string.Empty, MarkdownSanitizer.SanitizeContent("   ", _defaultOptions, false));
    }

    [Fact]
    public void SanitizeContent_WithPlainText_PreservesContent()
    {
        // Arrange
        var content = "This is plain text content.";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void SanitizeContent_WithValidMarkdown_PreservesMarkdown()
    {
        // Arrange
        var content = """
            # Heading
            
            This is **bold** and *italic* text.
            
            - List item
            
            > Quote
            """;

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void SanitizeContent_WithValidImage_PreservesImage()
    {
        // Arrange
        var content = "![Alt text](https://imgur.com/image.png)";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void SanitizeContent_WithInvalidImageDomain_ReplacesWithAltText()
    {
        // Arrange
        var content = "![Alt text](https://evil-site.com/image.png)";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal("[Image: Alt text]", result);
    }

    [Fact]
    public void SanitizeContent_WithValidLink_PreservesLink()
    {
        // Arrange
        var content = "[Link text](https://example.com)";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void SanitizeContent_WithJavaScriptLink_RemovesLink()
    {
        // Arrange
        var content = "[Click me](javascript:alert('xss'))";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal("Click me", result);
    }

    [Fact]
    public void SanitizeContent_WithHttpImageInDevelopment_PreservesImage()
    {
        // Arrange
        var content = "![Alt text](http://imgur.com/image.png)";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _developmentOptions, true);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void SanitizeContent_WithHttpImageInProduction_ReplacesWithAltText()
    {
        // Arrange
        var content = "![Alt text](http://imgur.com/image.png)";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal("[Image: Alt text]", result);
    }

    [Fact]
    public void SanitizeContent_WithValidBase64Image_PreservesImage()
    {
        // Arrange
        var content = "![Alt text](data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==)";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void SanitizeContent_WithOversizedBase64Image_ReplacesWithAltText()
    {
        // Arrange
        var largeBase64 = new string('A', 7000000); // ~7MB
        var content = $"![Alt text](data:image/png;base64,{largeBase64})";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal("[Image: Alt text]", result);
    }

    [Fact]
    public void SanitizeContent_WithScriptTags_RemovesScriptTags()
    {
        // Arrange
        var content = "This has <script>alert('xss')</script> content.";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal("This has  content.", result);
    }

    [Fact]
    public void SanitizeContent_WithExcessiveWhitespace_NormalizesWhitespace()
    {
        // Arrange
        var content = "This     has     excessive     whitespace.";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal("This    has    excessive    whitespace.", result);
    }

    [Fact]
    public void SanitizeNote_WithNullOrEmpty_ReturnsNull()
    {
        // Act & Assert
        Assert.Null(MarkdownSanitizer.SanitizeNote(null, _defaultOptions, false));
        Assert.Null(MarkdownSanitizer.SanitizeNote("", _defaultOptions, false));
        Assert.Null(MarkdownSanitizer.SanitizeNote("   ", _defaultOptions, false));
    }

    [Fact]
    public void SanitizeNote_WithValidContent_PreservesContent()
    {
        // Arrange
        var note = "This is a **bold** note.";

        // Act
        var result = MarkdownSanitizer.SanitizeNote(note, _defaultOptions, false);

        // Assert
        Assert.Equal(note, result);
    }

    [Fact]
    public void SanitizeNote_WithExcessiveWhitespace_NormalizesWhitespace()
    {
        // Arrange
        var note = "This   has   excessive   whitespace.";

        // Act
        var result = MarkdownSanitizer.SanitizeNote(note, _defaultOptions, false);

        // Assert
        Assert.Equal("This has excessive whitespace.", result);
    }

    [Fact]
    public void SanitizeNote_WithInvalidLink_RemovesLink()
    {
        // Arrange
        var note = "Check out [this link](javascript:alert('xss')).";

        // Act
        var result = MarkdownSanitizer.SanitizeNote(note, _defaultOptions, false);

        // Assert
        Assert.Equal("Check out this link.", result);
    }

    [Fact]
    public void SanitizeContent_WithComplexMixedContent_SanitizesCorrectly()
    {
        // Arrange
        var content = """
            # My Story
            
            ![Valid image](https://imgur.com/valid.png)
            ![Invalid image](https://evil-site.com/bad.png)
            
            [Valid link](https://example.com)
            [Invalid link](javascript:alert('xss'))
            
            This has <script>alert('bad')</script> script tags.
            
            This     has     excessive     whitespace.
            """;

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Contains("![Valid image](https://imgur.com/valid.png)", result);
        Assert.Contains("[Image: Invalid image]", result);
        Assert.Contains("[Valid link](https://example.com)", result);
        Assert.Contains("Invalid link", result);
        Assert.DoesNotContain("javascript:", result);
        Assert.DoesNotContain("<script>", result);
        Assert.DoesNotContain("     ", result); // Should be normalized to 4 spaces max
    }

    [Fact]
    public void SanitizeContent_WithImageTitle_PreservesTitle()
    {
        // Arrange
        var content = "![Alt text](https://imgur.com/image.png \"Image Title\")";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal(content, result);
    }

    [Fact]
    public void SanitizeContent_WithLinkTitle_PreservesTitle()
    {
        // Arrange
        var content = "[Link text](https://example.com \"Link Title\")";

        // Act
        var result = MarkdownSanitizer.SanitizeContent(content, _defaultOptions, false);

        // Assert
        Assert.Equal(content, result);
    }
}
