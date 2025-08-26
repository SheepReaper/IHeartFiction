using System.Text.RegularExpressions;

namespace IHFiction.SharedKernel.Validation;

/// <summary>
/// Shared generated regex patterns for input validation and sanitization across the application
/// </summary>
public static partial class ValidationRegexPatterns
{
    /// <summary>
    /// Matches any whitespace character for removal
    /// </summary>
    [GeneratedRegex(@"\s")]
    public static partial Regex WhitespaceCharacter();

    /// <summary>
    /// Matches one or more consecutive whitespace characters for normalization
    /// </summary>
    [GeneratedRegex(@"\s+")]
    public static partial Regex ConsecutiveWhitespace();

    /// <summary>
    /// Matches potentially harmful content (XSS patterns) - case insensitive
    /// </summary>
    [GeneratedRegex(@"<script|javascript:|on\w+\s*=", RegexOptions.IgnoreCase)]
    public static partial Regex HarmfulContent();

    /// <summary>
    /// Matches markdown image syntax: ![alt text](url "optional title")
    /// </summary>
    [GeneratedRegex(@"!\[([^\]]*)\]\(([^\s""]+)(?:\s+""([^""]*)"")?\)")]
    public static partial Regex MarkdownImage();

    /// <summary>
    /// Matches markdown link syntax: [text](url "optional title")
    /// </summary>
    [GeneratedRegex(@"(?<!!)\[([^\]]+)\]\(([^\s""]+)(?:\s+""([^""]*)"")?\)")]
    public static partial Regex MarkdownLink();

    /// <summary>
    /// Matches data URI scheme for base64 images
    /// </summary>
    [GeneratedRegex(@"^data:image\/(png|jpeg|jpg|gif|webp|svg\+xml);base64,([A-Za-z0-9+/=]+)$")]
    public static partial Regex Base64Image();

    /// <summary>
    /// Matches HTTP/HTTPS URLs
    /// </summary>
    [GeneratedRegex(@"^https?://[^\s<>""]+$", RegexOptions.IgnoreCase)]
    public static partial Regex HttpUrl();

    /// <summary>
    /// Matches potentially dangerous URL schemes
    /// </summary>
    [GeneratedRegex(@"^(javascript|data|file|ftp|mailto|tel):", RegexOptions.IgnoreCase)]
    public static partial Regex DangerousUrlScheme();

    /// <summary>
    /// Matches 3 or more consecutive whitespace characters
    /// </summary>
    [GeneratedRegex(@"\s{3,}")]
    public static partial Regex ExcessiveWhitespace3Plus();

    /// <summary>
    /// Matches 5 or more consecutive whitespace characters
    /// </summary>
    [GeneratedRegex(@"\s{5,}")]
    public static partial Regex ExcessiveWhitespace5Plus();

    /// <summary>
    /// Matches script tags with content - case insensitive
    /// </summary>
    [GeneratedRegex(@"<script.*?</script>", RegexOptions.IgnoreCase)]
    public static partial Regex ScriptTags();

    /// <summary>
    /// Matches javascript: protocol - case insensitive
    /// </summary>
    [GeneratedRegex(@"javascript:", RegexOptions.IgnoreCase)]
    public static partial Regex JavascriptProtocol();

    /// <summary>
    /// Matches event handler attributes - case insensitive
    /// </summary>
    [GeneratedRegex(@"on\w+\s*=", RegexOptions.IgnoreCase)]
    public static partial Regex EventHandlers();
}
