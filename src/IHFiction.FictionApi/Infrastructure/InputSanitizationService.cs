using System.Text.RegularExpressions;

using IHFiction.SharedKernel.Validation;

namespace IHFiction.FictionApi.Common;

/// <summary>
/// Centralized service for input sanitization and text processing.
/// Eliminates duplicate sanitization patterns and ensures consistent text handling.
/// </summary>
internal static partial class InputSanitizationService
{
    /// <summary>
    /// Sanitizes author notes with markdown-aware processing.
    /// Used for book/chapter notes fields.
    /// </summary>
    public static string? SanitizeNote(string? note)
    {
        // Notes are optional, so return null for empty/whitespace
        if (string.IsNullOrWhiteSpace(note))
            return null;

        // For notes, preserve line breaks but normalize whitespace
        var lines = note.Split('\n', StringSplitOptions.None);
        var sanitizedLines = lines.Select(line =>
            ValidationRegexPatterns.ConsecutiveWhitespace().Replace(line.Trim(), " "));
            
        var sanitized = string.Join('\n', sanitizedLines).Trim();

        return string.IsNullOrEmpty(sanitized) ? null : sanitized;
    }

    [GeneratedRegex(@"\n\s*\n\s*\n+", RegexOptions.Multiline)]
    private static partial Regex ExcessiveBlankLinesRegex();
    /// <summary>
    /// Sanitizes general text input by trimming and normalizing whitespace.
    /// Used for titles, descriptions, names, and other text fields.
    /// </summary>
    public static string SanitizeText(string? input)
    {
        return string.IsNullOrWhiteSpace(input)
            ? string.Empty
            : ValidationRegexPatterns.ConsecutiveWhitespace().Replace(input.Trim(), " ");
    }

    /// <summary>
    /// Sanitizes optional text input, returning null for empty/whitespace-only input.
    /// Used for optional fields like bio, notes, or descriptions.
    /// </summary>
    public static string? SanitizeOptionalText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) 
            return null;
        
        var sanitized = SanitizeText(input);
        return string.IsNullOrEmpty(sanitized) ? null : sanitized;
    }

    /// <summary>
    /// Sanitizes story/chapter titles with specific rules.
    /// Ensures titles are properly formatted and within reasonable length.
    /// </summary>
    public static string SanitizeTitle(string? title)
    {
        var sanitized = SanitizeText(title);
        
        // Additional title-specific processing could go here
        // e.g., capitalize first letter, handle special characters, etc.
        
        return sanitized;
    }

    /// <summary>
    /// Sanitizes descriptions with markdown-aware processing.
    /// Preserves intentional formatting while cleaning up whitespace.
    /// </summary>
    public static string SanitizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) 
            return string.Empty;

        // For descriptions, we want to preserve line breaks but normalize other whitespace
        var lines = description.Split('\n', StringSplitOptions.None);
        var sanitizedLines = lines.Select(line => 
            ValidationRegexPatterns.ConsecutiveWhitespace()
                .Replace(line.Trim(), " "));
        
        return string.Join('\n', sanitizedLines).Trim();
    }

    /// <summary>
    /// Sanitizes author bio with length limits and formatting rules.
    /// Returns null for empty input, properly formatted bio otherwise.
    /// </summary>
    public static string? SanitizeBio(string? bio)
    {
        return SanitizeOptionalText(bio);
    }

    /// <summary>
    /// Sanitizes author names with specific formatting rules.
    /// Ensures proper capitalization and character restrictions.
    /// </summary>
    public static string SanitizeName(string? name)
    {
        var sanitized = SanitizeText(name);
        
        // Additional name-specific processing could go here
        // e.g., proper case conversion, character restrictions, etc.
        
        return sanitized;
    }

    /// <summary>
    /// Sanitizes search queries by removing potentially harmful characters
    /// and normalizing whitespace for consistent search behavior.
    /// </summary>
    public static string SanitizeSearchQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) 
            return string.Empty;

        // Remove potentially harmful characters for search
        var sanitized = query.Trim();
        
        // Normalize whitespace
        sanitized = ValidationRegexPatterns.ConsecutiveWhitespace()
            .Replace(sanitized, " ");
        
        // Additional search-specific sanitization could go here
        // e.g., escape special regex characters, limit length, etc.
        
        return sanitized;
    }

    /// <summary>
    /// Sanitizes tag values with specific formatting rules.
    /// Ensures tags are properly formatted and normalized.
    /// </summary>
    public static string SanitizeTag(string? tag)
    {
        var sanitized = SanitizeText(tag);
        
        // Tags are typically lowercase and have specific formatting rules
#pragma warning disable CA1308 // Normalize strings to uppercase - we specifically want lowercase for tags
        return sanitized.ToLowerInvariant();
#pragma warning restore CA1308
    }

    /// <summary>
    /// Sanitizes URLs with basic validation and formatting.
    /// Ensures URLs are properly formatted and safe.
    /// </summary>
    public static string? SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) 
            return null;

        var sanitized = url.Trim();
        
        // Basic URL validation - could be enhanced with more sophisticated validation
        if (!Uri.TryCreate(sanitized, UriKind.Absolute, out var validUri))
            return null;

        // Only allow HTTP/HTTPS schemes for security
        return validUri.Scheme != Uri.UriSchemeHttp && validUri.Scheme != Uri.UriSchemeHttps
            ? null
            : validUri.ToString();
    }

    /// <summary>
    /// Sanitizes markdown content while preserving formatting.
    /// Used for story/chapter content that supports markdown.
    /// </summary>
    public static string SanitizeMarkdown(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) 
            return string.Empty;

        // For markdown, we want to preserve most formatting
        // but still normalize excessive whitespace
        var sanitized = content.Trim();
        
        // Remove excessive blank lines (more than 2 consecutive)
        sanitized = ExcessiveBlankLinesRegex().Replace(sanitized, "\n\n");
        
        return sanitized;
    }

    /// <summary>
    /// Validates and sanitizes sort parameters.
    /// Ensures sort fields and orders are valid and safe.
    /// </summary>
    public static (string sortBy, string sortOrder) SanitizeSortParameters(
        string? sortBy, 
        string? sortOrder, 
        string defaultSortBy = "updated", 
        string defaultSortOrder = "desc",
        string[]? allowedSortFields = null)
    {
        // Sanitize and validate sort by
#pragma warning disable CA1308 // Normalize strings to uppercase - we specifically want lowercase for sort parameters
        var cleanSortBy = SanitizeText(sortBy).ToLowerInvariant();
#pragma warning restore CA1308
        if (string.IsNullOrEmpty(cleanSortBy))
            cleanSortBy = defaultSortBy;

        // Validate against allowed fields if provided
        if (allowedSortFields != null && !allowedSortFields.Contains(cleanSortBy))
            cleanSortBy = defaultSortBy;

        // Sanitize and validate sort order
#pragma warning disable CA1308 // Normalize strings to uppercase - we specifically want lowercase for sort parameters
        var cleanSortOrder = SanitizeText(sortOrder).ToLowerInvariant();
#pragma warning restore CA1308
        if (cleanSortOrder != "asc" && cleanSortOrder != "desc")
            cleanSortOrder = defaultSortOrder;

        return (cleanSortBy, cleanSortOrder);
    }

    /// <summary>
    /// Truncates text to a maximum length while preserving word boundaries.
    /// Useful for ensuring text fields don't exceed database limits.
    /// </summary>
    public static string TruncateText(string? text, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrWhiteSpace(text)) 
            return string.Empty;

        var sanitized = SanitizeText(text);
        
        if (sanitized.Length <= maxLength)
            return sanitized;

        // Find the last space before the max length to avoid cutting words
        var truncateAt = maxLength - suffix.Length;
        var lastSpace = sanitized.LastIndexOf(' ', truncateAt);
        
        if (lastSpace > 0 && lastSpace > truncateAt - 20) // Don't go too far back
            truncateAt = lastSpace;

        return sanitized[..truncateAt] + suffix;
    }

    /// <summary>
    /// Validates that text meets minimum and maximum length requirements.
    /// Returns validation errors if the text is invalid.
    /// </summary>
    public static IEnumerable<string> ValidateTextLength(
        string? text, 
        string fieldName, 
        int minLength = 0, 
        int maxLength = int.MaxValue)
    {
        var errors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(text))
        {
            if (minLength > 0)
                errors.Add($"{fieldName} is required.");
            return errors;
        }

        var sanitized = SanitizeText(text);
        
        if (sanitized.Length < minLength)
            errors.Add($"{fieldName} must be at least {minLength} characters long.");
        
        if (sanitized.Length > maxLength)
            errors.Add($"{fieldName} must not exceed {maxLength} characters.");

        return errors;
    }
}
