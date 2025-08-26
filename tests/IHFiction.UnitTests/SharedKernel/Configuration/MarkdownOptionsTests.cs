using IHFiction.SharedKernel.Markdown;

namespace IHFiction.UnitTests.SharedKernel.Configuration;

public class MarkdownOptionsTests
{
    [Fact]
    public void DefaultOptions_HasCorrectDefaults()
    {
        // Arrange & Act
        var options = new MarkdownOptions();

        // Assert
        Assert.Equal(5 * 1024 * 1024, options.MaxBase64ImageSizeBytes); // 5MB
        Assert.False(options.AllowInsecureHttp);
        Assert.True(options.AllowAllHttpsLinks);
        Assert.False(options.AllowRelativeLinks);
        Assert.NotEmpty(options.AllowedImageDomains);
        Assert.Empty(options.AllowedLinkDomains);
    }

    [Fact]
    public void AllowedImageDomains_ContainsExpectedDomains()
    {
        // Arrange
        var options = new MarkdownOptions();

        // Assert - Check for some key domains
        Assert.Contains("imgur.com", options.AllowedImageDomains);
        Assert.Contains("i.imgur.com", options.AllowedImageDomains);
        Assert.Contains("github.com", options.AllowedImageDomains);
        Assert.Contains("raw.githubusercontent.com", options.AllowedImageDomains);
        Assert.Contains("images.unsplash.com", options.AllowedImageDomains);
        Assert.Contains("amazonaws.com", options.AllowedImageDomains);
        Assert.Contains("cloudfront.net", options.AllowedImageDomains);
    }

    [Fact]
    public void AllowedImageDomains_IsCaseInsensitive()
    {
        // Arrange
        var options = new MarkdownOptions();

        // Act & Assert
        Assert.Contains("IMGUR.COM", options.AllowedImageDomains);
        Assert.Contains("imgur.COM", options.AllowedImageDomains);
        Assert.Contains("ImGuR.cOm", options.AllowedImageDomains);
    }

    [Fact]
    public void AllowedLinkDomains_IsCaseInsensitive()
    {
        // Arrange
        var options = new MarkdownOptions();
        options.AllowedLinkDomains.Add("example.com");

        // Act & Assert
        Assert.Contains("EXAMPLE.COM", options.AllowedLinkDomains);
        Assert.Contains("example.COM", options.AllowedLinkDomains);
        Assert.Contains("ExAmPlE.cOm", options.AllowedLinkDomains);
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        // Assert
        Assert.Equal("Markdown", MarkdownOptions.SectionName);
    }

    [Fact]
    public void MaxBase64ImageSizeBytes_CanBeModified()
    {
        // Arrange
        var options = new MarkdownOptions();
        var newSize = 10 * 1024 * 1024; // 10MB

        // Act
        options.MaxBase64ImageSizeBytes = newSize;

        // Assert
        Assert.Equal(newSize, options.MaxBase64ImageSizeBytes);
    }

    [Fact]
    public void AllowHttpInDevelopment_CanBeModified()
    {
        // Arrange
        var options = new MarkdownOptions();

        // Act
        options.AllowInsecureHttp = true;

        // Assert
        Assert.True(options.AllowInsecureHttp);
    }

    [Fact]
    public void AllowAllHttpsLinks_CanBeModified()
    {
        // Arrange
        var options = new MarkdownOptions();

        // Act
        options.AllowAllHttpsLinks = false;

        // Assert
        Assert.False(options.AllowAllHttpsLinks);
    }

    [Fact]
    public void AllowRelativeLinks_CanBeModified()
    {
        // Arrange
        var options = new MarkdownOptions();

        // Act
        options.AllowRelativeLinks = true;

        // Assert
        Assert.True(options.AllowRelativeLinks);
    }

    [Fact]
    public void AllowedImageDomains_CanBeModified()
    {
        // Arrange
        var options = new MarkdownOptions();
        var originalCount = options.AllowedImageDomains.Count;

        // Act
        options.AllowedImageDomains.Add("custom-domain.com");

        // Assert
        Assert.Equal(originalCount + 1, options.AllowedImageDomains.Count);
        Assert.Contains("custom-domain.com", options.AllowedImageDomains);
    }

    [Fact]
    public void AllowedLinkDomains_CanBeModified()
    {
        // Arrange
        var options = new MarkdownOptions();

        // Act
        options.AllowedLinkDomains.Add("trusted-site.com");

        // Assert
        Assert.Single(options.AllowedLinkDomains);
        Assert.Contains("trusted-site.com", options.AllowedLinkDomains);
    }

    [Fact]
    public void AllowedImageDomains_ContainsPopularImageHosts()
    {
        // Arrange
        var options = new MarkdownOptions();

        // Assert - Verify we have good coverage of popular image hosting services
        var expectedDomains = new[]
        {
            "imgur.com",
            "i.imgur.com",
            "cdn.discordapp.com",
            "github.com",
            "raw.githubusercontent.com",
            "images.unsplash.com",
            "pixabay.com",
            "pexels.com",
            "flickr.com",
            "amazonaws.com",
            "cloudfront.net",
            "googleusercontent.com",
            "medium.com",
            "substackcdn.com"
        };

        foreach (var domain in expectedDomains)
        {
            Assert.Contains(domain, options.AllowedImageDomains);
        }
    }
}
