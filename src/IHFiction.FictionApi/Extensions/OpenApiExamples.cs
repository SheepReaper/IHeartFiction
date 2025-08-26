using System.Globalization;

namespace IHFiction.FictionApi.Extensions;

/// <summary>
/// Provides consistent example data for OpenAPI documentation across all endpoints.
/// </summary>
internal static class OpenApiExamples
{
    /// <summary>
    /// Example data for Author-related endpoints.
    /// </summary>
    internal static class Authors
    {
        /// <summary>
        /// Example ULID for author identification.
        /// </summary>
        public static readonly Ulid ExampleAuthorId = Ulid.Parse("01ARZ3NDEKTSV4RRFFQ69G5FAV", CultureInfo.InvariantCulture);

        /// <summary>
        /// Example ULID for work identification.
        /// </summary>
        public static readonly Ulid ExampleWorkId = Ulid.Parse("01ARZ3NDEKTSV4RRFFQ69G5FB0", CultureInfo.InvariantCulture);
        
        /// <summary>
        /// Example user GUID.
        /// </summary>
        public static readonly Guid ExampleUserId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");

        /// <summary>
        /// Example author bio content.
        /// </summary>
        public const string ExampleBio = "I'm a fantasy writer who loves creating magical worlds and complex characters. " +
                                        "I've been writing for over 10 years and have published several novels in the epic fantasy genre. " +
                                        "My works often explore themes of friendship, courage, and the battle between good and evil.";

        /// <summary>
        /// Example short bio for search results.
        /// </summary>
        public const string ExampleShortBio = "Fantasy author specializing in epic adventures and magical worlds.";

        /// <summary>
        /// Example author name.
        /// </summary>
        public const string ExampleAuthorName = "Jane Doe";

        /// <summary>
        /// Example email address.
        /// </summary>
        public const string ExampleEmail = "jane.doe@example.com";

        /// <summary>
        /// Example work title.
        /// </summary>
        public const string ExampleWorkTitle = "The Dragon's Quest: An Epic Fantasy Adventure";

        /// <summary>
        /// Example search query.
        /// </summary>
        public const string ExampleSearchQuery = "fantasy writer";

        /// <summary>
        /// Example timestamps.
        /// </summary>
        internal static class Timestamps
        {
            public static readonly DateTime CreatedAt = new(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            public static readonly DateTime UpdatedAt = new(2024, 7, 24, 14, 45, 0, DateTimeKind.Utc);
            public static readonly DateTime PublishedAt = new(2024, 3, 20, 9, 0, 0, DateTimeKind.Utc);
        }

        /// <summary>
        /// Example pagination values.
        /// </summary>
        internal static class Pagination
        {
            public const int DefaultPage = 1;
            public const int DefaultPageSize = 20;
            public const int SearchPageSize = 10;
            public const int TotalCount = 1;
            public const int TotalPages = 1;
        }

        /// <summary>
        /// Example story statistics.
        /// </summary>
        internal static class Statistics
        {
            public const int TotalStories = 5;
            public const int PublishedStories = 3;
        }
    }

    /// <summary>
    /// Example data for Story-related endpoints.
    /// </summary>
    internal static class Stories
    {
        /// <summary>
        /// Example ULID for story identification.
        /// </summary>
        public static readonly Ulid ExampleStoryId = Ulid.Parse("01ARZ3NDEKTSV4RRFFQ69G5FC0", CultureInfo.InvariantCulture);

        /// <summary>
        /// Example ULID for chapter identification.
        /// </summary>
        public static readonly Ulid ExampleChapterId = Ulid.Parse("01ARZ3NDEKTSV4RRFFQ69G5FD0", CultureInfo.InvariantCulture);

        /// <summary>
        /// Example story title.
        /// </summary>
        public const string ExampleStoryTitle = "The Dragon's Quest: An Epic Fantasy Adventure";

        /// <summary>
        /// Example story description.
        /// </summary>
        public const string ExampleStoryDescription = "Join young Aria as she embarks on a perilous journey to find the legendary Crystal of Eternity. " +
                                                     "With her loyal companions and magical abilities, she must overcome ancient curses, " +
                                                     "face powerful dragons, and discover the true meaning of courage in this epic fantasy tale.";

        /// <summary>
        /// Example chapter title.
        /// </summary>
        public const string ExampleChapterTitle = "Chapter 1: The Mysterious Portal";

        /// <summary>
        /// Example story content (markdown).
        /// </summary>
        public const string ExampleStoryContent = "# The Beginning\n\n" +
                                                 "The morning sun cast long shadows across the ancient forest as Aria stepped through the shimmering portal. " +
                                                 "She had trained for this moment her entire life, but nothing could have prepared her for what lay ahead.\n\n" +
                                                 "## The First Challenge\n\n" +
                                                 "As she emerged on the other side, the air crackled with magical energy. " +
                                                 "The landscape before her was unlike anything she had ever seen...";

        /// <summary>
        /// Example search query for stories.
        /// </summary>
        public const string ExampleSearchQuery = "fantasy adventure";

        /// <summary>
        /// Example author filter.
        /// </summary>
        public const string ExampleAuthorFilter = "Jane Doe";

        /// <summary>
        /// Example tags filter.
        /// </summary>
        public const string ExampleTagsFilter = "fantasy,adventure,magic";

        /// <summary>
        /// Example tag values.
        /// </summary>
        public static readonly string[] ExampleTags = ["fantasy", "adventure", "magic", "dragons"];

        /// <summary>
        /// Example timestamps for stories.
        /// </summary>
        internal static class Timestamps
        {
            public static readonly DateTime CreatedAt = new(2024, 2, 1, 9, 0, 0, DateTimeKind.Utc);
            public static readonly DateTime UpdatedAt = new(2024, 7, 24, 16, 30, 0, DateTimeKind.Utc);
            public static readonly DateTime PublishedAt = new(2024, 3, 15, 12, 0, 0, DateTimeKind.Utc);
            public static readonly DateTime ContentUpdatedAt = new(2024, 7, 24, 16, 45, 0, DateTimeKind.Utc);
        }

        /// <summary>
        /// Example pagination values for stories.
        /// </summary>
        internal static class Pagination
        {
            public const int DefaultPage = 1;
            public const int DefaultPageSize = 20;
            public const int TotalCount = 1;
            public const int TotalPages = 1;
            public const bool HasNextPage = false;
            public const bool HasPreviousPage = false;
        }

        /// <summary>
        /// Example story statistics.
        /// </summary>
        internal static class Statistics
        {
            public const int ContentLength = 1250;
            public const bool HasContent = true;
            public const bool HasChapters = true;
            public const bool HasBooks = false;
            public const bool IsValid = true;
        }
    }

    /// <summary>
    /// Example data for Tags-related endpoints.
    /// </summary>
    internal static class Tags
    {
        /// <summary>
        /// Example tag category.
        /// </summary>
        public const string ExampleCategory = "genre";

        /// <summary>
        /// Example tag subcategory.
        /// </summary>
        public const string ExampleSubcategory = "subgenre";

        /// <summary>
        /// Example tag value.
        /// </summary>
        public const string ExampleTagValue = "fantasy";

        /// <summary>
        /// Example tag search query.
        /// </summary>
        public const string ExampleSearchQuery = "fantasy";

        /// <summary>
        /// Example tag values for different categories.
        /// </summary>
        public static readonly string[] ExampleGenreTags = ["fantasy", "science-fiction", "romance", "mystery"];
        public static readonly string[] ExampleThemeTags = ["adventure", "coming-of-age", "redemption", "friendship"];
        public static readonly string[] ExampleSettingTags = ["medieval", "modern", "futuristic", "urban"];

        /// <summary>
        /// Example pagination values for tags.
        /// </summary>
        internal static class Pagination
        {
            public const int DefaultPage = 1;
            public const int DefaultPageSize = 50;
            public const int TotalCount = 1;
            public const int TotalPages = 1;
        }
    }

    /// <summary>
    /// Example data for Chapter-related endpoints.
    /// </summary>
    internal static class Chapters
    {
        /// <summary>
        /// Example ULID for chapter identification.
        /// </summary>
        public static readonly Ulid ExampleChapterId = Ulid.Parse("01ARZ3NDEKTSV4RRFFQ69G5FE0", CultureInfo.InvariantCulture);

        /// <summary>
        /// Example chapter title.
        /// </summary>
        public const string ExampleChapterTitle = "Chapter 1: The Mysterious Portal";

        /// <summary>
        /// Example chapter content (markdown).
        /// </summary>
        public const string ExampleChapterContent = "# The Beginning\n\n" +
                                                   "The morning sun cast long shadows across the ancient forest as Aria stepped through the shimmering portal. " +
                                                   "She had trained for this moment her entire life, but nothing could have prepared her for what lay ahead.\n\n" +
                                                   "## The First Challenge\n\n" +
                                                   "As she emerged on the other side, the air crackled with magical energy...";

        /// <summary>
        /// Example chapter note.
        /// </summary>
        public const string ExampleChapterNote = "**Author's Note:** This chapter introduces the main character and sets up the magical world.";

        /// <summary>
        /// Example timestamps for chapters.
        /// </summary>
        internal static class Timestamps
        {
            public static readonly DateTime CreatedAt = new(2024, 2, 15, 10, 0, 0, DateTimeKind.Utc);
            public static readonly DateTime UpdatedAt = new(2024, 7, 24, 17, 0, 0, DateTimeKind.Utc);
            public static readonly DateTime PublishedAt = new(2024, 3, 20, 14, 0, 0, DateTimeKind.Utc);
            public static readonly DateTime ContentUpdatedAt = new(2024, 7, 24, 17, 15, 0, DateTimeKind.Utc);
        }

        /// <summary>
        /// Example chapter statistics.
        /// </summary>
        internal static class Statistics
        {
            public const int ContentLength = 2500;
            public const bool HasContent = true;
        }
    }

    /// <summary>
    /// Example data for validation error responses.
    /// </summary>
    internal static class ValidationErrors
    {
        /// <summary>
        /// Example validation problem details for bad requests.
        /// </summary>
        public static readonly object ExampleValidationProblem = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            title = "One or more validation errors occurred.",
            status = 400,
            errors = new Dictionary<string, string[]>
            {
                ["Bio"] = ["Bio cannot exceed 2000 characters.", "Bio contains potentially harmful content."],
                ["Page"] = ["Page must be greater than 0."],
                ["PageSize"] = ["Page size must be between 1 and 100."]
            }
        };

        /// <summary>
        /// Example search query validation error.
        /// </summary>
        public static readonly object ExampleSearchValidationError = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            title = "One or more validation errors occurred.",
            status = 400,
            errors = new Dictionary<string, string[]>
            {
                ["Q"] = ["Search query is required.", "Search query must be between 2 and 100 characters."]
            }
        };
    }

    /// <summary>
    /// Example data for error responses.
    /// </summary>
    internal static class ErrorResponses
    {
        /// <summary>
        /// Example unauthorized error response.
        /// </summary>
        public static readonly object ExampleUnauthorized = new
        {
            type = "https://tools.ietf.org/html/rfc7235#section-3.1",
            title = "Unauthorized",
            status = 401,
            detail = "Authentication is required to access this resource."
        };

        /// <summary>
        /// Example not found error response.
        /// </summary>
        public static readonly object ExampleNotFound = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            title = "Not Found",
            status = 404,
            detail = "The requested author was not found."
        };

        /// <summary>
        /// Example conflict error response.
        /// </summary>
        public static readonly object ExampleConflict = new
        {
            type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            title = "Conflict",
            status = 409,
            detail = "The author profile has been modified by another user. Please refresh and try again."
        };
    }

    /// <summary>
    /// Example data for request parameters.
    /// </summary>
    internal static class Parameters
    {
        /// <summary>
        /// Example pagination parameters.
        /// </summary>
        internal static class Pagination
        {
            public const int ExamplePage = 1;
            public const int ExamplePageSize = 20;
            public const string ExampleSortBy = "name";
            public const string ExampleSortOrder = "asc";
        }

        /// <summary>
        /// Example search parameters.
        /// </summary>
        internal static class Search
        {
            public const string ExampleQuery = "fantasy author";
            public const string ExampleSearchTerm = "epic fantasy";
        }

        /// <summary>
        /// Example sorting parameters.
        /// </summary>
        internal static class Sorting
        {
            public static readonly string[] AllowedSortFields = ["name", "created", "updated"];
            public static readonly string[] AllowedSortOrders = ["asc", "desc"];
        }
    }
}
