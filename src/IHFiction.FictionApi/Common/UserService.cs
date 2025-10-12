using System.Security.Claims;

using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.Infrastructure;

namespace IHFiction.FictionApi.Common;

internal sealed class UserService(FictionDbContext context)
{
    internal static class Errors
    {
        public static readonly DomainError UserNotFound = new("User.NotFound", "User not found.");
        public static readonly DomainError UserAlreadyExists = new("User.AlreadyExists", "User already exists.");
        public static readonly DomainError AuthorNotFound = new("Author.NotFound", "Author not found.");
        public static readonly DomainError UserNotAuthor = new("User.NotAuthor", "User is not an author.");
    }

    internal async Task<Result<User>> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        return user is null ? Errors.UserNotFound : user;
    }

    internal async Task<Result<User>> GetUserAsync(Ulid id, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

        return user is null ? Errors.UserNotFound : user;
    }

    internal async Task<Result<User>> GetUserAsync(ClaimsPrincipal claims, CancellationToken cancellationToken = default)
    {
        var userIdResult = claims.GetUid();

        return userIdResult.IsFailure
            ? userIdResult.DomainError
            : await GetUserAsync(userIdResult.Value, cancellationToken);
    }

    internal Task<Result<User>> CreateUserAsync(Guid userId, string? displayName = null, CancellationToken cancellationToken = default) =>
        InsertUserAsync(() => User.FromUserId(userId, displayName), cancellationToken);

    internal async Task<Result<User>> GetOrCreateUserAsync(ClaimsPrincipal claims, CancellationToken cancellationToken = default)
    {
        var userIdResult = claims.GetUid();

        if (userIdResult.IsFailure) return userIdResult.DomainError;

        var userResult = await GetUserAsync(userIdResult.Value, cancellationToken);

        if (userResult.IsSuccess) return userResult;

        var usernameResult = claims.GetUsername();

        return await InsertUserAsync(() => User.FromUserId(userIdResult.Value, usernameResult.IsSuccess ? usernameResult.Value : null), cancellationToken);

        // return await CreateUserAsync(userIdResult.Value, usernameResult.IsSuccess ? usernameResult.Value : null, cancellationToken);
    }

    internal async Task<Result<User>> GetOrCreateUserAsync(Guid userId, Func<User> userFactory, CancellationToken cancellationToken)
    {
        var userResult = await GetUserAsync(userId, cancellationToken);

        return userResult ? userResult : await InsertUserAsync(userFactory, cancellationToken);
    }

    private async Task<Result<User>> InsertUserAsync(Func<User> userFactory, CancellationToken cancellationToken)
    {
        var user = userFactory();

        await context.Users.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return user;
    }

    internal async Task<Result<Author>> PromoteToAuthorAsync(User user, CancellationToken cancellationToken)
    {
        // Edge Case: find and detach any potentially tracked Profile for this user's ID.
        var existingProfile = context.ChangeTracker.Entries<Profile>()
            .FirstOrDefault(e => e.Property<Ulid>("AuthorId").CurrentValue == user.Id);

        // Create a new Author entity
        var author = Author.FromUser(user);

        if (existingProfile != null)
        {
            existingProfile.State = EntityState.Detached;
            author.Profile = existingProfile.Entity;
        }

        // Detach the main user entity
        context.Entry(user).State = EntityState.Detached;
        // context.Users.Remove(user);

        // Explicitly create new Profile?
        // Should be handled by the Author constructor

        // Attach the new Author entity (use update in lieu of attach)
        var entry = context.Entry(author);
        // context.Authors.Add(author);

        context.Authors.Update(author);

        entry.Property<string>("Discriminator").CurrentValue = nameof(Author);
        entry.Property<string>("Discriminator").IsModified = true;

        // Should generate an update statement instead of an insert
        await context.SaveChangesAsync(cancellationToken);

        return author;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>")]
    internal async Task<Result<Author>> GetAuthorAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var author = await context.Authors
            .Where(a => a.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);

        return author is null ? Errors.AuthorNotFound : author;
    }

    internal async Task<Result<Author>> GetAuthorAsync(Ulid id, CancellationToken cancellationToken = default)
    {
        var author = await context.Authors
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

        return author is null ? Errors.AuthorNotFound : author;
    }

    internal async Task<Result<Author>> GetAuthorAsync(ClaimsPrincipal claims, CancellationToken cancellationToken = default)
    {
        var userIdResult = claims.GetUid();

        return userIdResult.IsFailure
            ? userIdResult.DomainError
            : await GetAuthorAsync(userIdResult.Value, cancellationToken);
    }
}
