namespace IHFiction.SharedWeb.Components.Reading;

public sealed record ReaderChapterNavItem(
    string Id,
    string Title,
    int Order,
    string? BookTitle = null);
