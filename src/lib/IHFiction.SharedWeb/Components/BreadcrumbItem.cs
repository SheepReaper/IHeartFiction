namespace IHFiction.SharedWeb.Components;

public readonly record struct BreadcrumbItem(string Text, string? Href = null, bool IsActive = false);
