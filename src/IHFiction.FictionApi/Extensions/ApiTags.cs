namespace IHFiction.FictionApi.Extensions;

/// <summary>
/// Centralized tag management system for OpenAPI documentation.
/// Provides hierarchical organization and metadata optimized for Scalar interface display.
/// </summary>
internal static class ApiTags
{
    /// <summary>
    /// Author-related endpoint tags.
    /// </summary>
    internal static class Authors
    {
        /// <summary>
        /// Public author information and discovery endpoints.
        /// </summary>
        public const string Discovery = "Author Discovery";

        /// <summary>
        /// Authenticated author management endpoints.
        /// </summary>
        public const string Management = "Author Management";
    }

    /// <summary>
    /// Book-related endpoint tags.
    /// </summary>
    internal static class Books
    {
        /// <summary>
        /// Book management and content endpoints.
        /// </summary>
        public const string Management = "Book Management";
    }

    /// <summary>
    /// Story-related endpoint tags.
    /// </summary>
    internal static class Stories
    {
        /// <summary>
        /// Public story browsing and discovery endpoints.
        /// </summary>
        public const string Discovery = "Story Discovery";

        /// <summary>
        /// Authenticated story management endpoints.
        /// </summary>
        public const string Management = "Story Management";
    }

    /// <summary>
    /// Tag system endpoint tags.
    /// </summary>
    internal static class Tags
    {
        /// <summary>
        /// Tag browsing and management endpoints.
        /// </summary>
        public const string Discovery = "Tag Discovery";
    }

    /// <summary>
    /// User account and profile endpoint tags.
    /// </summary>
    internal static class Account
    {
        /// <summary>
        /// Current user profile and account management endpoints.
        /// </summary>
        public const string CurrentUser = "Current User";
    }

    /// <summary>
    /// Chapter-related endpoint tags.
    /// </summary>
    internal static class Chapters
    {
        /// <summary>
        /// Chapter management and content endpoints.
        /// </summary>
        public const string Management = "Chapter Management";
    }

    /// <summary>
    /// Metadata for all API tags, optimized for Scalar interface display.
    /// </summary>
    /// <param name="Name">The tag name</param>
    /// <param name="Description">Description of the tag's purpose</param>
    /// <param name="Order">Display order in documentation (lower numbers appear first)</param>
    internal record TagMetadata(string Name, string Description, int Order);

    /// <summary>
    /// Gets all tag metadata for documentation generation.
    /// Ordered for optimal display in Scalar interface.
    /// </summary>
    /// <returns>Array of tag metadata ordered by display priority</returns>
    public static TagMetadata[] GetAllTags() =>
    [
        new(Stories.Discovery, "Public story browsing, search, and discovery. No authentication required.", 1),
        new(Authors.Discovery, "Public author information, profiles, and discovery. No authentication required.", 2),
        new(Tags.Discovery, "Tag browsing and filtering for content discovery. No authentication required.", 3),
        new(Account.CurrentUser, "Current user profile and account management. Requires authentication.", 4),
        new(Authors.Management, "Author profile management and registration. Requires authentication.", 5),
        new(Stories.Management, "Story creation, editing, and management. Requires authentication and author role.", 6),
        new(Books.Management, "Book creation, editing, and management. Requires authentication and appropriate permissions.", 7),
        new(Chapters.Management, "Chapter creation, editing, and content management. Requires authentication and appropriate permissions.", 8)
    ];

    /// <summary>
    /// Gets tags that require authentication.
    /// </summary>
    /// <returns>Array of tag names that require authentication</returns>
    public static string[] GetAuthenticatedTags() =>
    [
        Account.CurrentUser,
        Authors.Management,
        Stories.Management,
        Books.Management,
        Chapters.Management
    ];

    /// <summary>
    /// Gets tags that are public (no authentication required).
    /// </summary>
    /// <returns>Array of tag names that are public</returns>
    public static string[] GetPublicTags() =>
    [
        Stories.Discovery,
        Authors.Discovery,
        Tags.Discovery
    ];

    /// <summary>
    /// Checks if a tag requires authentication.
    /// </summary>
    /// <param name="tagName">The tag name to check</param>
    /// <returns>True if the tag requires authentication, false otherwise</returns>
    public static bool RequiresAuthentication(string tagName) =>
        GetAuthenticatedTags().Contains(tagName);

    /// <summary>
    /// Gets the display order for a tag.
    /// </summary>
    /// <param name="tagName">The tag name</param>
    /// <returns>The display order, or int.MaxValue if tag not found</returns>
    public static int GetDisplayOrder(string tagName) =>
        GetAllTags().FirstOrDefault(t => t.Name == tagName)?.Order ?? int.MaxValue;
}
