using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;


using IHFiction.SharedKernel.Validation;

namespace IHFiction.SharedKernel.Markdown;

/// <summary>
/// Validates that a string contains safe markdown content with proper image and link validation
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ValidMarkdownAttribute : ValidationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidMarkdownAttribute"/> class.
    /// </summary>
    public ValidMarkdownAttribute()
    {
        ErrorMessage = "{0} contains invalid or unsafe markdown content.";
    }

    /// <summary>
    /// Validates the specified value with respect to the current validation attribute.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="validationContext">The context information about the validation operation.</param>
    /// <returns>An instance of the <see cref="ValidationResult"/> class.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null or "")
        {
            return ValidationResult.Success;
        }

        if (value is not string stringValue)
        {
            return new ValidationResult("Value must be a string.");
        }

        try
        {
            // Use default options for now - this will be enhanced when we add DI support
            var markdownOptions = new MarkdownOptions();
            var isDevelopment = false; // Conservative default

            var validationResult = ValidateMarkdownContent(stringValue, markdownOptions, isDevelopment);
            return !validationResult.IsValid ? new ValidationResult(validationResult.ErrorMessage) : ValidationResult.Success;
        }
        catch (ArgumentException ex)
        {
            return new ValidationResult($"Invalid markdown content: {ex.Message}");
        }
        catch (FormatException ex)
        {
            return new ValidationResult($"Invalid format in markdown content: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates markdown content for security and compliance
    /// </summary>
    private static MarkdownValidationResult ValidateMarkdownContent(string content, MarkdownOptions options, bool isDevelopment)
    {
        // Check for basic harmful content first
        if (ValidationRegexPatterns.HarmfulContent().IsMatch(content))
        {
            return MarkdownValidationResult.Invalid("Content contains potentially harmful scripts or code.");
        }

        // Validate images
        var imageMatches = ValidationRegexPatterns.MarkdownImage().Matches(content);
        foreach (Match match in imageMatches)
        {
            var url = match.Groups[2].Value.Trim();
            var imageValidation = ValidateImageUrl(url, options, isDevelopment);
            if (!imageValidation.IsValid)
            {
                return imageValidation;
            }
        }

        // Validate links
        var linkMatches = ValidationRegexPatterns.MarkdownLink().Matches(content);
        foreach (Match match in linkMatches)
        {
            var url = match.Groups[2].Value.Trim();
            var linkValidation = ValidateLinkUrl(url, options, isDevelopment);
            if (!linkValidation.IsValid)
            {
                return linkValidation;
            }
        }

        return MarkdownValidationResult.Valid();
    }

    /// <summary>
    /// Validates an image URL for security and compliance
    /// </summary>
    private static MarkdownValidationResult ValidateImageUrl(string url, MarkdownOptions options, bool isDevelopment)
    {
        // Check for dangerous URL schemes
        if (ValidationRegexPatterns.DangerousUrlScheme().IsMatch(url))
        {
            // Special case: allow data URIs for base64 images
            return url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? ValidateBase64Image(url, options)
                : MarkdownValidationResult.Invalid($"Image URL uses dangerous scheme: {url}");
        }

        // Validate HTTP/HTTPS URLs
        return ValidationRegexPatterns.HttpUrl().IsMatch(url)
            ? ValidateRemoteImageUrl(url, options, isDevelopment)
            : MarkdownValidationResult.Invalid($"Invalid image URL format: {url}");
    }

    /// <summary>
    /// Validates a base64 embedded image
    /// </summary>
    private static MarkdownValidationResult ValidateBase64Image(string dataUri, MarkdownOptions options)
    {
        var match = ValidationRegexPatterns.Base64Image().Match(dataUri);
        if (!match.Success)
        {
            return MarkdownValidationResult.Invalid("Invalid base64 image format. Only PNG, JPEG, GIF, WebP, and SVG images are allowed.");
        }

        var base64Data = match.Groups[2].Value;
        
        try
        {
            // Calculate approximate size (base64 is ~33% larger than binary)
            var approximateSize = (base64Data.Length * 3) / 4;
            
            if (approximateSize > options.MaxBase64ImageSizeBytes)
            {
                var maxSizeMB = options.MaxBase64ImageSizeBytes / (1024.0 * 1024.0);
                return MarkdownValidationResult.Invalid($"Base64 image exceeds maximum size of {maxSizeMB:F1}MB.");
            }

            // Validate base64 format
            Convert.FromBase64String(base64Data);
            
            return MarkdownValidationResult.Valid();
        }
        catch (FormatException)
        {
            return MarkdownValidationResult.Invalid("Invalid base64 image data.");
        }
    }

    /// <summary>
    /// Validates a remote image URL
    /// </summary>
    private static MarkdownValidationResult ValidateRemoteImageUrl(string url, MarkdownOptions options, bool isDevelopment)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return MarkdownValidationResult.Invalid($"Invalid image URL: {url}");
        }

        // Check protocol
        var isHttpsAllowed = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        var isHttpAllowed = uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && isDevelopment && options.AllowInsecureHttp;

        if (!isHttpsAllowed && !isHttpAllowed)
        {
            var requiredProtocol = isDevelopment && options.AllowInsecureHttp ? "HTTP or HTTPS" : "HTTPS";
            return MarkdownValidationResult.Invalid($"Image URLs must use {requiredProtocol} protocol: {url}");
        }

        // Check domain whitelist
        var domain = uri.Host.ToUpperInvariant();
        return !options.AllowedImageDomains.Contains(domain)
            ? MarkdownValidationResult.Invalid($"Image domain not allowed: {domain}")
            : MarkdownValidationResult.Valid();
    }

    /// <summary>
    /// Validates a link URL
    /// </summary>
    private static MarkdownValidationResult ValidateLinkUrl(string url, MarkdownOptions options, bool isDevelopment)
    {
        // Check for dangerous URL schemes
        if (ValidationRegexPatterns.DangerousUrlScheme().IsMatch(url))
        {
            return MarkdownValidationResult.Invalid($"Link URL uses dangerous scheme: {url}");
        }

        // Allow relative links if configured
        if (options.AllowRelativeLinks && !url.Contains("://", StringComparison.Ordinal))
        {
            return MarkdownValidationResult.Valid();
        }

        // Validate HTTP/HTTPS URLs
        if (!ValidationRegexPatterns.HttpUrl().IsMatch(url))
        {
            return MarkdownValidationResult.Invalid($"Invalid link URL format: {url}");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return MarkdownValidationResult.Invalid($"Invalid link URL: {url}");
        }

        // Check protocol (only HTTPS for links, or HTTP in development)
        var isHttpsAllowed = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        var isHttpAllowed = uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && isDevelopment && options.AllowInsecureHttp;

        if (!isHttpsAllowed && !isHttpAllowed)
        {
            var requiredProtocol = isDevelopment && options.AllowInsecureHttp ? "HTTP or HTTPS" : "HTTPS";
            return MarkdownValidationResult.Invalid($"Link URLs must use {requiredProtocol} protocol: {url}");
        }

        // Check domain whitelist if configured
        if (!options.AllowAllHttpsLinks && options.AllowedLinkDomains.Count > 0)
        {
            var domain = uri.Host.ToUpperInvariant();
            if (!options.AllowedLinkDomains.Contains(domain))
            {
                return MarkdownValidationResult.Invalid($"Link domain not allowed: {domain}");
            }
        }

        return MarkdownValidationResult.Valid();
    }

    /// <summary>
    /// Result of markdown validation
    /// </summary>
    private readonly struct MarkdownValidationResult
    {
        public bool IsValid { get; }
        public string? ErrorMessage { get; }

        private MarkdownValidationResult(bool isValid, string? errorMessage = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public static MarkdownValidationResult Valid() => new(true);
        public static MarkdownValidationResult Invalid(string errorMessage) => new(false, errorMessage);
    }
}
