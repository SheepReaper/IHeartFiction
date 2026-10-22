namespace IHFiction.SharedWeb.Services;

public static class ReaderRoutes
{
    public static string? ContainerDestination(LinkedOfGetPublishedWorkMetaResponse meta)
    {
        ArgumentNullException.ThrowIfNull(meta);
        if (meta.IsDirectlyReadable || meta.StoryId is null) return null;
        return meta.WorkType switch
        {
            "Story" => $"/stories/{meta.StoryId}",
            "Book" => $"/stories/{meta.StoryId}#book-{meta.Id}",
            _ => null
        };
    }
}
