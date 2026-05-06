namespace IHFiction.SharedKernel.Stories;

public static class StoryCoverRules
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public static IReadOnlyList<string> AllowedContentTypes { get; } = [
        "image/jpeg",
        "image/png",
        "image/webp"
    ];

    public static IReadOnlyList<string> AllowedFileExtensions { get; } = [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    ];

    public static string AcceptAttribute => string.Join(",", AllowedFileExtensions);

    public static bool IsAllowedContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType)
        && AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    public static bool IsAllowedFileExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return AllowedFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}