namespace IHFiction.SharedWeb.Services;

/// <summary>
/// Manages reader progress (last read chapter) for a story using protected browser storage.
/// </summary>
public sealed class ReaderProgressService(BrowserProtectedStorageService storage)
{
    private static string BuildKey(string storyId) => $"reader:lastRead:{storyId}";

    /// <summary>
    /// Retrieves the ID of the last chapter read for a story, or null if not found.
    /// </summary>
    public async Task<string?> GetLastReadChapterIdAsync(string storyId)
    {
        if (string.IsNullOrWhiteSpace(storyId))
        {
            return null;
        }

        var value = await storage.GetAsync<string>(BuildKey(storyId));
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Persists the last chapter read for a story.
    /// </summary>
    public async Task SetLastReadChapterIdAsync(string storyId, string chapterId)
    {
        if (string.IsNullOrWhiteSpace(storyId) || string.IsNullOrWhiteSpace(chapterId))
        {
            return;
        }

        await storage.SetAsync(BuildKey(storyId), chapterId);
    }

    /// <summary>
    /// Clears the persisted last read chapter for a story.
    /// </summary>
    public async Task ClearLastReadChapterIdAsync(string storyId)
    {
        if (string.IsNullOrWhiteSpace(storyId))
        {
            return;
        }

        await storage.DeleteAsync(BuildKey(storyId));
    }
}
