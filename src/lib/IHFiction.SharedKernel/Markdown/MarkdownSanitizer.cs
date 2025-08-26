using IHFiction.SharedKernel.Validation;

namespace IHFiction.SharedKernel.Markdown;

/// <summary>
/// Provides markdown-aware sanitization that preserves valid markdown syntax while removing harmful content
/// </summary>
public static class MarkdownSanitizer
{
    /// <summary>
    /// Sanitizes markdown content by preserving valid markdown syntax while removing harmful content
    /// </summary>
    /// <param name="content">The markdown content to sanitize</param>
    /// <param name="options">Markdown configuration options</param>
    /// <param name="isDevelopment">Whether the application is running in development mode</param>
    /// <returns>Sanitized markdown content</returns>
    public static string SanitizeContent(string? content, MarkdownOptions options, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        var sanitized = content.Trim();

        // Step 1: Remove basic harmful content (but preserve markdown syntax)
        sanitized = RemoveHarmfulContent(sanitized);

        // Step 2: Sanitize images while preserving valid ones
        sanitized = SanitizeImages(sanitized, options, isDevelopment);

        // Step 3: Sanitize links while preserving valid ones
        sanitized = SanitizeLinks(sanitized, options, isDevelopment);

        // Step 4: Normalize whitespace while preserving markdown formatting
        sanitized = NormalizeWhitespace(sanitized);

        return sanitized;
    }

    /// <summary>
    /// Sanitizes note content with more aggressive whitespace normalization
    /// </summary>
    /// <param name="note">The note content to sanitize</param>
    /// <param name="options">Markdown configuration options</param>
    /// <param name="isDevelopment">Whether the application is running in development mode</param>
    /// <returns>Sanitized note content</returns>
    public static string? SanitizeNote(string? note, MarkdownOptions options, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(note))
            return null;

        var sanitized = note.Trim();

        // Remove harmful content
        sanitized = RemoveHarmfulContent(sanitized);

        // Sanitize images and links (notes can have them too)
        sanitized = SanitizeImages(sanitized, options, isDevelopment);
        sanitized = SanitizeLinks(sanitized, options, isDevelopment);

        // More aggressive whitespace normalization for notes
        sanitized = ValidationRegexPatterns.ConsecutiveWhitespace().Replace(sanitized, " ");

        return string.IsNullOrEmpty(sanitized) ? null : sanitized;
    }

    /// <summary>
    /// Removes harmful content while preserving markdown syntax
    /// </summary>
    private static string RemoveHarmfulContent(string content)
    {
        // Remove script tags
        content = ValidationRegexPatterns.ScriptTags().Replace(content, "");

        // Remove event handlers (but be careful not to break markdown)
        content = ValidationRegexPatterns.EventHandlers().Replace(content, "");

        return content;
    }

    /// <summary>
    /// Sanitizes markdown images, removing invalid ones
    /// </summary>
    private static string SanitizeImages(string content, MarkdownOptions options, bool isDevelopment)
    {
        return ValidationRegexPatterns.MarkdownImage().Replace(content, match =>
        {
            var altText = match.Groups[1].Value;
            var url = match.Groups[2].Value.Trim();
            var title = match.Groups[3].Success ? match.Groups[3].Value : null;

            if (IsValidImageUrl(url, options, isDevelopment))
            {
                // Reconstruct the valid image markdown
                var titlePart = !string.IsNullOrEmpty(title) ? $" \"{title}\"" : "";
                return $"![{altText}]({url}{titlePart})";
            }

            // Replace invalid image with alt text in brackets
            return $"[Image: {altText}]";
        });
    }

    /// <summary>
    /// Sanitizes markdown links, removing invalid ones
    /// </summary>
    private static string SanitizeLinks(string content, MarkdownOptions options, bool isDevelopment)
    {
        return ValidationRegexPatterns.MarkdownLink().Replace(content, match =>
        {
            var linkText = match.Groups[1].Value;
            var url = match.Groups[2].Value.Trim();
            var title = match.Groups[3].Success ? match.Groups[3].Value : null;

            if (IsValidLinkUrl(url, options, isDevelopment))
            {
                // Reconstruct the valid link markdown
                var titlePart = !string.IsNullOrEmpty(title) ? $" \"{title}\"" : "";
                return $"[{linkText}]({url}{titlePart})";
            }

            // Replace invalid link with just the link text
            return linkText;
        });
    }

    /// <summary>
    /// Normalizes whitespace while preserving markdown formatting
    /// </summary>
    private static string NormalizeWhitespace(string content)
    {
        // Preserve double line breaks (paragraph breaks in markdown)
        // Replace 5+ consecutive whitespace with 4 spaces (preserve some formatting)
        content = ValidationRegexPatterns.ExcessiveWhitespace5Plus().Replace(content, "    ");

        return content;
    }

    /// <summary>
    /// Validates if an image URL is safe and allowed
    /// </summary>
    private static bool IsValidImageUrl(string url, MarkdownOptions options, bool isDevelopment)
    {
        // Check for dangerous schemes first
        if (ValidationRegexPatterns.DangerousUrlScheme().IsMatch(url))
        {
            // Special case: allow valid base64 images
            return url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && IsValidBase64Image(url, options);
        }

        // Check HTTP/HTTPS URLs
        return ValidationRegexPatterns.HttpUrl().IsMatch(url) && IsValidRemoteImageUrl(url, options, isDevelopment);
    }

    /// <summary>
    /// Validates if a link URL is safe and allowed
    /// </summary>
    private static bool IsValidLinkUrl(string url, MarkdownOptions options, bool isDevelopment)
    {
        // Check for dangerous schemes
        if (ValidationRegexPatterns.DangerousUrlScheme().IsMatch(url))
        {
            return false;
        }

        // Allow relative links if configured
        if (options.AllowRelativeLinks && !url.Contains("://", StringComparison.Ordinal))
        {
            return true;
        }

        // Check HTTP/HTTPS URLs
        if (!ValidationRegexPatterns.HttpUrl().IsMatch(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // Check protocol
        var isHttpsAllowed = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        var isHttpAllowed = uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && isDevelopment && options.AllowInsecureHttp;

        if (!isHttpsAllowed && !isHttpAllowed)
        {
            return false;
        }

        // Check domain whitelist if configured
        if (!options.AllowAllHttpsLinks && options.AllowedLinkDomains.Count > 0)
        {
            var domain = uri.Host.ToUpperInvariant();
            return options.AllowedLinkDomains.Contains(domain);
        }

        return true;
    }

    /// <summary>
    /// Validates a base64 image
    /// </summary>
    private static bool IsValidBase64Image(string dataUri, MarkdownOptions options)
    {
        var match = ValidationRegexPatterns.Base64Image().Match(dataUri);
        if (!match.Success)
        {
            return false;
        }

        var base64Data = match.Groups[2].Value;

        try
        {
            // Calculate approximate size
            var approximateSize = (base64Data.Length * 3) / 4;
            if (approximateSize > options.MaxBase64ImageSizeBytes)
            {
                return false;
            }

            // Validate base64 format
            Convert.FromBase64String(base64Data);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates a remote image URL
    /// </summary>
    private static bool IsValidRemoteImageUrl(string url, MarkdownOptions options, bool isDevelopment)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // Check protocol
        var isHttpsAllowed = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        var isHttpAllowed = uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && isDevelopment && options.AllowInsecureHttp;

        if (!isHttpsAllowed && !isHttpAllowed)
        {
            return false;
        }

        // Check domain whitelist
        var domain = uri.Host.ToUpperInvariant();
        return options.AllowedImageDomains.Contains(domain);
    }
}
