using System.Security.Claims;

using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Authors;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Common;

/// <summary>
/// Centralized service for handling authentication and authorization logic across endpoints.
/// Eliminates duplicate auth patterns and provides consistent permission checking.
/// </summary>
internal sealed class AuthorizationService(FictionDbContext context, UserService userService)
{
    internal static class Errors
    {
        // Use common errors for infrastructure concerns
        public static readonly DomainError UserNotAuthor = CommonErrors.Author.NotRegistered;
        public static readonly DomainError DatabaseError = CommonErrors.Database.ConnectionFailed;
        public static readonly DomainError StoryNotFound = CommonErrors.Story.NotFound;

        // Authorization-specific errors
        public static readonly DomainError ChapterNotFound = new("Authorization.ChapterNotFound", "Chapter not found.");
        public static readonly DomainError OwnerOnlyOperation = new("Authorization.OwnerOnlyOperation", "Only the owner can perform this operation.");
        public static readonly DomainError CollaboratorRequired = new("Authorization.CollaboratorRequired", "You must be the owner or a collaborator to perform this operation.");
        public static readonly DomainError InsufficientPermissions = CommonErrors.Auth.InsufficientPermissions;
    }
    /// <summary>
    /// Gets the current authenticated author from claims principal.
    /// Standardizes the author retrieval pattern used across multiple endpoints.
    /// </summary>
    public async Task<Result<Author>> GetCurrentAuthorAsync(
        ClaimsPrincipal claimsPrincipal, 
        CancellationToken cancellationToken = default)
    {
        var authorResult = await userService.GetAuthorAsync(claimsPrincipal, cancellationToken);
        return authorResult.IsFailure ? Errors.UserNotAuthor : authorResult.Value;
    }

    /// <summary>
    /// Authorizes story access and returns the story, author, and calculated permissions.
    /// Centralizes the story authorization pattern used in UpdateStoryMetadata, DeleteStory, etc.
    /// </summary>
    public async Task<Result<StoryAuthorizationResult>> AuthorizeStoryAccessAsync(
        Ulid storyId, 
        ClaimsPrincipal claimsPrincipal, 
        StoryAccessLevel requiredAccess,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        // Get current author
        var authorResult = await GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);
        if (authorResult.IsFailure) return authorResult.DomainError;

        var author = authorResult.Value;

        // Load story with related data
        var query = context.Stories
            .Include(s => s.Owner)
            .Include(s => s.Authors)
            .AsQueryable();

        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        var story = await query.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);

        if (story is null)
            return Errors.StoryNotFound;

        // Calculate permissions
        var permissions = CalculateStoryPermissions(story, author);

        // Check if user has required access level
        return !permissions.HasAccess(requiredAccess)
          ? (Result<StoryAuthorizationResult>)(requiredAccess switch
          {
              StoryAccessLevel.Delete or StoryAccessLevel.Publish => Errors.OwnerOnlyOperation,
              StoryAccessLevel.Edit => Errors.CollaboratorRequired,
              _ => Errors.InsufficientPermissions
          })
          : (Result<StoryAuthorizationResult>)new StoryAuthorizationResult(story, author, permissions);
    }

    /// <summary>
    /// Authorizes chapter access and returns the chapter, author, and calculated permissions.
    /// Centralizes the chapter authorization pattern used in UpdateChapterContent, etc.
    /// </summary>
    public async Task<Result<ChapterAuthorizationResult>> AuthorizeChapterAccessAsync(
        Ulid chapterId,
        ClaimsPrincipal claimsPrincipal,
        StoryAccessLevel requiredAccess,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        // Get current author
        var authorResult = await GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);
        if (authorResult.IsFailure) return authorResult.DomainError;

        var author = authorResult.Value;

        // Load chapter with related data
        var query = context.Chapters
            .Include(c => c.Owner)
            .Include(c => c.Authors)
            .AsQueryable();

        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        var chapter = await query.FirstOrDefaultAsync(c => c.Id == chapterId, cancellationToken);

        if (chapter is null)
            return Errors.ChapterNotFound;

        // Calculate permissions (chapters inherit story permissions)
        var permissions = CalculateChapterPermissions(chapter, author);

        // Check if user has required access level
        return !permissions.HasAccess(requiredAccess)
          ? (Result<ChapterAuthorizationResult>)(requiredAccess switch
          {
              StoryAccessLevel.Delete => Errors.OwnerOnlyOperation,
              StoryAccessLevel.Edit => Errors.CollaboratorRequired,
              _ => Errors.InsufficientPermissions
          })
          : (Result<ChapterAuthorizationResult>)new ChapterAuthorizationResult(chapter, author, permissions);
    }

    public async Task<Result<BookAuthorizationResult>> AuthorizeBookAccessAsync(Ulid bookId, ClaimsPrincipal claimsPrincipal, StoryAccessLevel requiredAccess, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var authorResult = await GetCurrentAuthorAsync(claimsPrincipal, cancellationToken);

        if (authorResult.IsFailure) return authorResult.DomainError;

        var author = authorResult.Value;
        var query = context.Books.Include(b => b.Owner).Include(b => b.Authors).Include(b => b.Story).AsQueryable();

        if (includeDeleted) query = query.IgnoreQueryFilters();

        var book = await query.FirstOrDefaultAsync(b => b.Id == bookId, cancellationToken);

        if (book is null) return Errors.StoryNotFound;
        if (book.Story is null) return Errors.StoryNotFound;

        var storyPermissions = CalculateStoryPermissions(book.Story, author);
        
        return !storyPermissions.HasAccess(requiredAccess)
          ? (Result<BookAuthorizationResult>)(requiredAccess switch
          {
              StoryAccessLevel.Delete or StoryAccessLevel.Publish => Errors.OwnerOnlyOperation,
              StoryAccessLevel.Edit => Errors.CollaboratorRequired,
              _ => Errors.InsufficientPermissions
          })
          : (Result<BookAuthorizationResult>)new BookAuthorizationResult(book, author, storyPermissions);
    }

    /// <summary>
    /// Calculates story permissions for a given author.
    /// </summary>
    private static StoryPermissions CalculateStoryPermissions(Story story, Author author)
    {
        var isOwner = story.OwnerId == author.Id;
        var isCollaborator = story.Authors.Any(a => a.Id == author.Id);
        
        return new StoryPermissions(
          IsOwner: isOwner,
          IsCollaborator: isCollaborator,
          CanRead: isOwner || isCollaborator,
          CanEdit: isOwner || isCollaborator,
          CanDelete: isOwner,
          CanPublish: isOwner
        );
    }

    /// <summary>
    /// Calculates chapter permissions for a given author.
    /// </summary>
    private static StoryPermissions CalculateChapterPermissions(Chapter chapter, Author author)
    {
        var isOwner = chapter.OwnerId == author.Id;
        var isCollaborator = chapter.Authors.Any(a => a.Id == author.Id);

        return new StoryPermissions(
          IsOwner: isOwner,
          IsCollaborator: isCollaborator,
          CanRead: isOwner || isCollaborator,
          CanEdit: isOwner || isCollaborator,
          CanDelete: isOwner,
          CanPublish: isOwner
        );
    }

    internal record BookAuthorizationResult(Book Book, Author Author, StoryPermissions Permissions);
}

/// <summary>
/// Defines the level of access required for story operations.
/// </summary>
internal enum StoryAccessLevel
{
    Read,
    Edit,
    Delete,
    Publish
}

/// <summary>
/// Represents the permissions a user has for a story or chapter.
/// </summary>
internal record StoryPermissions(
  bool IsOwner,
  bool IsCollaborator,
  bool CanRead,
  bool CanEdit,
  bool CanDelete,
  bool CanPublish)
{
    public bool HasAccess(StoryAccessLevel level) => level switch
    {
        StoryAccessLevel.Read => CanRead,
        StoryAccessLevel.Edit => CanEdit,
        StoryAccessLevel.Delete => CanDelete,
        StoryAccessLevel.Publish => CanPublish,
        _ => false
    };
}

/// <summary>
/// Result of story authorization containing the story, author, and permissions.
/// </summary>
internal record StoryAuthorizationResult(Story Story, Author Author, StoryPermissions Permissions);

/// <summary>
/// Result of chapter authorization containing the chapter, author, and permissions.
/// </summary>
internal record ChapterAuthorizationResult(Chapter Chapter, Author Author, StoryPermissions Permissions);
