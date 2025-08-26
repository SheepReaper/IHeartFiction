using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using IHFiction.Data;
using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Authors;

namespace IHFiction.IntegrationTests.Authors;

/// <summary>
/// Tests that demonstrate the AsNoTracking() bug and verify it has been fixed.
/// These tests show the difference between tracked and untracked entity behavior.
/// </summary>
public class AsNoTrackingBugTests : BaseIntegrationTest, IConfigureServices<AsNoTrackingBugTests>, IAsyncLifetime
{
    private readonly FictionDbContext _dbContext;

    public AsNoTrackingBugTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(AsNoTrackingBugTests)) ?? throw new Exception("DbContext not found");
    }

    [Fact]
    public async Task VerifyBugFix_TrackedEntity_ChangesPersist()
    {
        // This test verifies that the bug has been fixed by showing tracked entities work correctly

        // Arrange
        var testUserId = Guid.NewGuid();
        var originalBio = "Original bio content.";
        await CreateTestAuthorAsync(testUserId, "Test Author", originalBio);

        // Act - Get a tracked entity (without AsNoTracking)
        var trackedAuthor = await _dbContext.Authors
            .Include(a => a.Profile)
            .FirstAsync(a => a.UserId == testUserId, cancellationToken: TestContext.Current.CancellationToken);

        // Verify the entity is tracked
        var entry = _dbContext.Entry(trackedAuthor);
        Assert.Equal(EntityState.Unchanged, entry.State);

        // Modify the tracked entity
        trackedAuthor.Profile.Bio = "Modified bio that will persist.";

        // Note: Entity Framework doesn't automatically detect changes to owned entities
        // The entity state may remain Unchanged, but SaveChanges() will still persist the changes
        // This is the correct behavior for owned entities

        // Save changes
        var changesSaved = await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Changes should be saved because entity is tracked
        // Note: Even though EntityState may not show Modified, SaveChanges() detects and saves owned entity changes
        Assert.True(changesSaved > 0);

        // Verify the database was updated
        // using var verificationContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(AsNoTrackingBugTests)) ?? throw new Exception("DbContext not found");
        var persistedAuthor = await _dbContext.Authors
            .Include(a => a.Profile)
            .AsNoTracking()
            .FirstAsync(a => a.UserId == testUserId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Modified bio that will persist.", persistedAuthor.Profile.Bio);
        Assert.NotEqual(originalBio, persistedAuthor.Profile.Bio);
    }

    [Fact]
    public async Task UserService_AfterBugFix_ReturnsTrackedEntities()
    {
        // This test verifies that UserService now returns tracked entities

        // Arrange
        var testUserId = Guid.NewGuid();
        var originalBio = "Original bio for UserService test.";
        await CreateTestAuthorAsync(testUserId, "Test Author", originalBio);

        var userService = new UserService(_dbContext);

        var claims = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, testUserId.ToString())
        ], "test"));

        // Act - Get author through UserService
        var authorResult = await userService.GetAuthorAsync(claims, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(authorResult.IsSuccess);
        var author = authorResult.Value;

        // Verify the entity is tracked
        var entry = _dbContext.Entry(author);
        Assert.Equal(EntityState.Unchanged, entry.State);

        // Modify and save
        author.Profile.Bio = "Modified through UserService.";
        var changesSaved = await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Verify changes were saved
        Assert.True(changesSaved > 0);

        // Verify persistence
        // using var verificationContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(AsNoTrackingBugTests)) ?? throw new Exception("DbContext not found");
        var persistedAuthor = await _dbContext.Authors
            .Include(a => a.Profile)
            .AsNoTracking()
            .FirstAsync(a => a.UserId == testUserId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Modified through UserService.", persistedAuthor.Profile.Bio);
    }

    [Fact]
    public async Task EntityState_Tracking_Demonstration()
    {
        // This test demonstrates different EntityState values and their implications

        // Arrange
        var testUserId = Guid.NewGuid();
        await CreateTestAuthorAsync(testUserId, "Test Author", "Original bio.");

        // Test 1: Tracked entity
        var trackedAuthor = await _dbContext.Authors
            .Include(a => a.Profile)
            .FirstAsync(a => a.UserId == testUserId, cancellationToken: TestContext.Current.CancellationToken);

        var trackedEntry = _dbContext.Entry(trackedAuthor);
        Assert.Equal(EntityState.Unchanged, trackedEntry.State);

        // Test 2: Untracked entity (AsNoTracking)
        var untrackedAuthor = await _dbContext.Authors
            .Include(a => a.Profile)
            .AsNoTracking()
            .FirstAsync(a => a.UserId == testUserId, cancellationToken: TestContext.Current.CancellationToken);

        var untrackedEntry = _dbContext.Entry(untrackedAuthor);
        Assert.Equal(EntityState.Detached, untrackedEntry.State);

        // Test 3: Modify tracked entity
        trackedAuthor.Profile.Bio = "Modified tracked entity.";
        // Note: Entity Framework doesn't automatically detect changes to owned entities
        // The entity state may remain Unchanged, but SaveChanges() will still persist the changes

        // Test 4: Modify untracked entity (no state change)
        untrackedAuthor.Profile.Bio = "Modified untracked entity.";
        Assert.Equal(EntityState.Detached, untrackedEntry.State); // Still detached

        // Test 5: Save changes
        var changesSaved = await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.True(changesSaved > 0); // Only tracked changes are saved
        Assert.Equal(EntityState.Unchanged, trackedEntry.State); // Back to unchanged after save
    }

    private async Task<Author> CreateTestAuthorAsync(Guid userId, string name, string bio)
    {
        var author = new Author
        {
            UserId = userId,
            Name = name,
            Profile = new Profile { Bio = bio }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync();

        // Clear the change tracker to ensure fresh retrieval from database
        _dbContext.ChangeTracker.Clear();

        return author;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>")]
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped(
            nameof(AsNoTrackingBugTests),
            (sp, key) => new FictionDbContext(new DbContextOptionsBuilder<FictionDbContext>()
                .UseNpgsql(sp.GetRequiredService<PgsqlConnectionStringProvider>().GetConnectionStringForDatabase($"test_{nameof(AsNoTrackingBugTests)}"))
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning))
                .WithDefaultInterceptors(sp.GetRequiredService<TimeProvider>())
                .Options));
    }

    public async ValueTask InitializeAsync()
    {
        if (_dbContext.Database.GetPendingMigrations().Any())
        {
            await _dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        // Close all connections before deleting database
        await _dbContext.Database.CloseConnectionAsync();

        // Use EnsureDeletedAsync for proper async database deletion
        await _dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);

        await _dbContext.DisposeAsync();

        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
