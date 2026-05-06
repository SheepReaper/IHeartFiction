namespace IHFiction.SharedKernel.Stories;

public static class StoryCoverRoutes
{
    public static string GetPath(Ulid storyId) => $"/stories/{storyId}/cover";
}