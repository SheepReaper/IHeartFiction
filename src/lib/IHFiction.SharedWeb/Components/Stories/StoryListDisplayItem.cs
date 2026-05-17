namespace IHFiction.SharedWeb.Components.Stories;

public enum StoryListViewMode
{
    Compact,
    Expanded
}

public sealed record StoryListDisplayItem(
    Ulid Id,
    string Title,
    string Href,
    string? AuthorName = null,
    string? AuthorHref = null,
    string? Description = null,
    string? CoverImagePath = null,
    DateTimeOffset? PublishedAt = null,
    DateTimeOffset? UpdatedAt = null,
    string? ContentKindLabel = null,
    string? StatusLabel = null,
    IReadOnlyList<string>? Badges = null,
    string? PrimaryActionHref = null,
    string? PrimaryActionLabel = null,
    string? PrimaryActionIcon = null,
    bool CanRead = false,
    bool IsPublished = false,
    bool HasBooks = false,
    bool HasChapters = false);