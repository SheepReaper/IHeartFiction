namespace IHFiction.FictionApi.Common;

/// <summary>Consumer-facing API reference groups, independent of source folders and authorization.</summary>
internal static class ApiTags
{
    public const string Stories = "Stories";
    public const string Chapters = "Chapters";
    public const string Books = "Books";
    public const string Works = "Works";
    public const string Authors = "Authors";
    public const string Tags = "Tags";
    public const string UserProfile = "User Profile";
    public const string Follows = "Follows";
    public const string Notifications = "Notifications";
    public const string PushSubscriptions = "Push Subscriptions";
    public const string AgentAuthentication = "Agent Authentication";

    private static readonly string[] ReferenceOrder =
    [
        Stories, Books, Chapters, Works, Authors, Tags,
        Follows, Notifications, PushSubscriptions, UserProfile, AgentAuthentication
    ];

    public static int GetDisplayOrder(string? name)
    {
        var index = Array.IndexOf(ReferenceOrder, name);
        return index >= 0 ? index : int.MaxValue;
    }

    public static string? GetDescription(string? name) => name switch
    {
        Stories => "Browse published stories and manage your own stories, including metadata, content, covers, tag assignments, structure, and publication. Private and editing operations require the permissions specified by each endpoint.",
        Chapters => "List a story's published chapters, create chapters within stories or books, and retrieve or edit chapter metadata and content. Published content and private draft content have separate endpoints.",
        Books => "Create books within a story and retrieve or update your own book metadata and chapter structure.",
        Works => "Operations shared across story, book, and chapter identifiers: resolve published metadata and readable content, record qualified reads, or publish a work. See individual endpoints for supported work types and permissions.",
        Authors => "Browse public author profiles, register the current user as an author, and manage your own author profile.",
        Tags => "Browse and resolve tags, or administer canonical tags and synonyms. Administrative listing, renaming, and merging require the admin role. Assign tags to a story through the Stories endpoints.",
        UserProfile => "Retrieve or update the authenticated user's display name and profile settings. Author-specific profiles are documented under Authors.",
        Follows => "Follow or unfollow authors and stories, and list follows for the authenticated user or an anonymous device. Device operations require the documented device identifier.",
        Notifications => "Retrieve notification inboxes and mark notifications as read for the authenticated user or an anonymous device. Manage follow relationships under Follows and delivery subscriptions under Push Subscriptions.",
        PushSubscriptions => "Register or refresh browser push-delivery subscriptions for authenticated users or anonymous devices. These subscriptions configure delivery; follow relationships are managed under Follows.",
        AgentAuthentication => "Confirm the link between an agent identity and a user account. Agent protocol discovery, registration, and token exchange are documented in /auth.md.",
        _ => null
    };
}
