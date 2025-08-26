namespace IHFiction.SharedKernel.Markdown;

/// <summary>
/// Configuration options for markdown content processing and validation
/// </summary>
public sealed class MarkdownOptions
{
    /// <summary>
    /// Configuration section name for binding
    /// </summary>
    public const string SectionName = "Markdown";

    /// <summary>
    /// Maximum size in bytes for base64 embedded images (default: 5MB)
    /// </summary>
    public long MaxBase64ImageSizeBytes { get; set; } = 5 * 1024 * 1024; // 5MB

    /// <summary>
    /// Whether to allow HTTP URLs for images in development mode (default: false)
    /// </summary>
    public bool AllowInsecureHttp { get; set; }

    /// <summary>
    /// List of allowed domains for remote images
    /// </summary>
    public HashSet<string> AllowedImageDomains { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        // Popular image hosting services
        "imgur.com",
        "i.imgur.com",
        "cdn.discordapp.com",
        "media.discordapp.net",
        "github.com",
        "raw.githubusercontent.com",
        "user-images.githubusercontent.com",
        "avatars.githubusercontent.com",
        "images.unsplash.com",
        "unsplash.com",
        "pixabay.com",
        "pexels.com",
        "flickr.com",
        "live.staticflickr.com",
        "farm1.staticflickr.com",
        "farm2.staticflickr.com",
        "farm3.staticflickr.com",
        "farm4.staticflickr.com",
        "farm5.staticflickr.com",
        "farm6.staticflickr.com",
        "farm7.staticflickr.com",
        "farm8.staticflickr.com",
        "farm9.staticflickr.com",
        "i.postimg.cc",
        "postimg.cc",
        "imagebin.ca",
        "tinypic.com",
        "photobucket.com",
        "s3.amazonaws.com",
        "amazonaws.com",
        "cloudfront.net",
        "googleusercontent.com",
        "ggpht.com",
        "blogspot.com",
        "wordpress.com",
        "wp.com",
        "medium.com",
        "miro.medium.com",
        "cdn-images-1.medium.com",
        "substackcdn.com",
        "substack.com"
    };

    /// <summary>
    /// List of allowed domains for links (empty means all HTTPS links allowed)
    /// </summary>
    public HashSet<string> AllowedLinkDomains { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether to allow all HTTPS links (default: true)
    /// </summary>
    public bool AllowAllHttpsLinks { get; set; } = true;

    /// <summary>
    /// Whether to allow relative links (default: false)
    /// </summary>
    public bool AllowRelativeLinks { get; set; }
}
