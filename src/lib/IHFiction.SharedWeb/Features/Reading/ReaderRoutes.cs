namespace IHFiction.SharedWeb.Features.Reading;

public static class ReaderRoutes
{
    public static string? ContainerDestination(LinkedOfGetPublishedWorkMetaResponse meta)
    {
        ArgumentNullException.ThrowIfNull(meta);
        if (meta.IsDirectlyReadable) return null;
        if (meta.DefaultReadableWorkId is { } defaultWorkId && defaultWorkId != Ulid.Empty)
        {
            return $"/read/{defaultWorkId}";
        }

        if (meta.StoryId is null) return null;
        return meta.WorkType switch
        {
            "Story" => $"/stories/{meta.StoryId}",
            "Book" => $"/stories/{meta.StoryId}#book-{meta.Id}",
            _ => null
        };
    }
}
