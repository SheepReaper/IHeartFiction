namespace IHFiction.SharedWeb.Components.Stories;

public static class StoryListDisplayAdapters
{
    public static StoryListDisplayItem FromAuthorStory(LinkedOfAuthorStoryItem story)
    {
        ArgumentNullException.ThrowIfNull(story);

        string? contentKindLabel = null;
        if (story.HasChapters)
        {
            contentKindLabel = "Chaptered story";
        }
        else if (story.HasBooks)
        {
            contentKindLabel = "Multi-book series";
        }
        else if (story.HasContent)
        {
            contentKindLabel = "One-shot";
        }

        return new(
            Id: story.Id,
            Title: story.Title,
            Href: $"/stories/{story.Id}",
            Description: string.IsNullOrWhiteSpace(story.Description) ? null : story.Description,
            CoverImagePath: story.HasCoverImage ? $"/stories/{story.Id}/cover" : null,
            PublishedAt: story.PublishedAt,
            UpdatedAt: story.UpdatedAt,
            ContentKindLabel: contentKindLabel,
            StatusLabel: story.IsPublished ? "Published" : "Draft",
            Badges: BuildMyStoriesBadges(story),
            PrimaryActionHref: $"/stories/{story.Id}/edit",
            PrimaryActionLabel: "Edit",
            PrimaryActionIcon: "pen",
            CanRead: story.IsPublished,
            IsPublished: story.IsPublished,
            HasBooks: story.HasBooks,
            HasChapters: story.HasChapters);
    }

    public static StoryListDisplayItem FromAuthorWorkItem(AuthorWorkItem work)
    {
        ArgumentNullException.ThrowIfNull(work);

        return new(
            Id: work.Id,
            Title: work.Title,
            Href: $"/stories/{work.Id}",
            PublishedAt: work.PublishedAt,
            StatusLabel: work.PublishedAt is null ? "Draft" : "Published",
            PrimaryActionHref: $"/stories/{work.Id}",
            PrimaryActionLabel: "View",
            PrimaryActionIcon: "eye",
            CanRead: work.PublishedAt is not null,
            IsPublished: work.PublishedAt is not null);
    }

    private static List<string> BuildMyStoriesBadges(LinkedOfAuthorStoryItem story)
    {
        List<string> badges = [];

        if (story.IsOwned)
        {
            badges.Add("Owner");
        }
        else
        {
            badges.Add("Collaborator");
        }

        if (story.CollaboratorNames.Count > 0)
        {
            badges.Add($"{story.CollaboratorNames.Count} collaborators");
        }

        return badges;
    }
}