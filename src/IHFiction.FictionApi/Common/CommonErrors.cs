using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Common;

/// <summary>
/// Common errors that appear across multiple use cases.
/// These represent cross-cutting concerns and infrastructure-level errors.
/// </summary>
internal static class CommonErrors
{
    /// <summary>
    /// Database-related errors that can occur in any use case involving persistence.
    /// </summary>
    internal static class Database
    {
        public static readonly DomainError SaveFailed = new("Database.SaveFailed", "A database error occurred while saving changes.");
        public static readonly DomainError ConcurrencyConflict = new("Database.ConcurrencyConflict", "The resource was modified by another user. Please refresh and try again.");
        public static readonly DomainError ConnectionFailed = new("Database.ConnectionFailed", "Failed to connect to the database.");
    }

    /// <summary>
    /// Author-related errors that can occur across multiple story and author operations.
    /// </summary>
    internal static class Author
    {
        public static readonly DomainError NotFound = new("Author.NotFound", "Author not found.");
        public static readonly DomainError NotRegistered = new("Author.NotRegistered", "You must be registered as an author to perform this action.");
        public static readonly DomainError NotAuthorized = new("Author.NotAuthorized", "You are not authorized to perform this action.");
    }

    /// <summary>
    /// Story-related errors that can occur across multiple story operations.
    /// </summary>
    internal static class Story
    {
        public static readonly DomainError NotFound = new("Story.NotFound", "Story not found.");
        public static readonly DomainError TitleExists = new("Story.Exists", "A story with this title already exists.");
        public static readonly DomainError AlreadyDeleted = new("Story.AlreadyDeleted", "Story has already been deleted.");
        public static readonly DomainError NotPublished = new("Story.NotPublished", "Story is not published.");

    }

    /// <summary>
    /// Authentication and authorization errors.
    /// </summary>
    internal static class Auth
    {
        public static readonly DomainError UserNotFound = new("Auth.NotFound", "User not found.");
        public static readonly DomainError InvalidClaims = new("Auth.InvalidClaims", "Invalid or missing authentication claims.");
        public static readonly DomainError InsufficientPermissions = new("Auth.InsufficientPermissions", "Insufficient permissions to perform this action.");
    }

    /// <summary>
    /// Chapter-related errors that can occur across multiple chapter operations.
    /// </summary>
    internal static class Chapter
    {
        public static readonly DomainError NotFound = new("Chapter.NotFound", "Chapter not found.");
        public static readonly DomainError AlreadyDeleted = new("Chapter.AlreadyDeleted", "Chapter has already been deleted.");
        public static readonly DomainError NotPublished = new("Chapter.NotPublished", "Chapter is not published.");
        public static readonly DomainError NoContent = new("Chapter.NoContent", "Chapter does not have content yet.");
    }

    internal static class Book{
        public static readonly DomainError NotFound = new("Book.NotFound", "Book not found.");
        public static readonly DomainError AlreadyDeleted = new("Book.AlreadyDeleted", "Book has already been deleted.");
        public static readonly DomainError NotPublished = new("Book.NotPublished", "Book is not published.");
        public static readonly DomainError NoContent = new("Book.NoContent", "Book does not have content yet.");

        public static readonly DomainError NotAuthorized = new("Book.NotAuthorized", "You are not authorized to perform this action.");

    }
}
