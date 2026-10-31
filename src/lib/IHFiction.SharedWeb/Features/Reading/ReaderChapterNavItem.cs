namespace IHFiction.SharedWeb.Features.Reading;

public sealed record ReaderChapterNavItem(
    string Id,
    string Title,
    int Order,
    string? BookTitle = null,
    int ReadCount = 0);
