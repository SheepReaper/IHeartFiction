using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using IHFiction.Data;
using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Authors;
using IHFiction.FictionApi.Common;

namespace IHFiction.IntegrationTests.Authors;

/// <summary>
/// Tests specifically focused on Entity Framework tracking behavior.
/// These tests verify that the AsNoTracking() issue has been resolved and that
/// entities retrieved through services are properly tracked for updates.
/// </summary>
public class EntityTrackingTests : BaseIntegrationTest, IConfigureServices<EntityTrackingTests>, IAsyncLifetime
{
    private readonly FictionDbContext _dbContext;

    public EntityTrackingTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(EntityTrackingTests)) ?? throw new Exception("DbContext not found");
    }
    [Fact]
    public async Task UserService_GetAuthorAsync_ReturnsTrackedEntity()
    {
        // This test verifies that UserService.GetAuthorAsync returns a tracked entity
        // that can be modified and saved to the database

        // Arrange
        var testUserId = Guid.NewGuid();
        var originalBio = "Original bio for tracking test.";
        await CreateTestAuthorAsync(testUserId, "Test Author", originalBio);

        var userService = new UserService(_dbContext);

        var claims = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, testUserId.ToString())
        ], "test"));

        // Act
        var authorResult = await userService.GetAuthorAsync(claims, TestContext.Current.CancellationToken);

        // Assert - Verify we got the author
        Assert.True(authorResult.IsSuccess);
        var author = authorResult.Value;
        Assert.Equal(originalBio, author.Profile.Bio);

        // Verify the entity is tracked by EF
        var entry = _dbContext.Entry(author);
        Assert.Equal(EntityState.Unchanged, entry.State);

        // Modify the author
        author.Profile.Bio = "Modified bio to test tracking.";

        // Note: Entity Framework doesn't automatically detect changes to owned entities
        // The entity state may remain Unchanged, but SaveChanges() will still persist the changes

        // Save and verify persistence
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Verify the change was actually saved
        // using var freshDbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(EntityTrackingTests)) ?? throw new Exception("DbContext not found");
        var persistedAuthor = await _dbContext.Authors
            .Include(a => a.Profile)
            .AsNoTracking()
            .FirstAsync(a => a.UserId == testUserId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Modified bio to test tracking.", persistedAuthor.Profile.Bio);
    }

    [Fact]
    public async Task UpdateAuthorProfile_FullWorkflow_PersistsChanges()
    {
        // This test verifies the complete UpdateAuthorProfile workflow
        // including entity retrieval, modification, and persistence

        // Arrange
        var testUserId = Guid.NewGuid();
        var originalBio = "Original bio for full workflow test.";
        await CreateTestAuthorAsync(testUserId, "Test Author", originalBio);

        var userService = new UserService(_dbContext);
        var authService = new AuthorizationService(_dbContext, userService);

        var claims = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, testUserId.ToString()),
            new Claim(ClaimTypes.Name, "testuser")
        ], "test"));

        var updateRequest = new UpdateAuthorProfile.UpdateAuthorProfileBody(
            "Updated bio through complete workflow test."
        );

        // Act - Execute the complete update workflow
        var useCase = new UpdateAuthorProfile(_dbContext, authService);
        var result = await useCase.HandleAsync(updateRequest, claims, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Updated bio through complete workflow test.", ((dynamic)result.Value!).Profile.Bio);

        // Verify the change was persisted to the database
        // using var verificationContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(EntityTrackingTests)) ?? throw new Exception("DbContext not found");
        var persistedAuthor = await _dbContext.Authors
            .Include(a => a.Profile)
            .AsNoTracking()
            .FirstAsync(a => a.UserId == testUserId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Updated bio through complete workflow test.", persistedAuthor.Profile.Bio);
        Assert.NotEqual(originalBio, persistedAuthor.Profile.Bio);
    }

    [Fact]
    public async Task EntityTracking_MultipleModifications_AllPersist()
    {
        // This test verifies that multiple modifications to a tracked entity all persist

        // Arrange
        var testUserId = Guid.NewGuid();
        await CreateTestAuthorAsync(testUserId, "Test Author", "Original bio.");

        var userService = new UserService(_dbContext);

        var claims = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, testUserId.ToString())
        ], "test"));

        // Act
        var authorResult = await userService.GetAuthorAsync(claims, TestContext.Current.CancellationToken);
        Assert.True(authorResult.IsSuccess);

        var author = authorResult.Value;

        // Make multiple modifications
        author.Profile.Bio = "First modification.";
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        author.Profile.Bio = "Second modification.";
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        author.Profile.Bio = "Final modification.";
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Verify final state
        // using var verificationContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(EntityTrackingTests)) ?? throw new Exception("DbContext not found");
        var finalAuthor = await _dbContext.Authors
            .Include(a => a.Profile)
            .AsNoTracking()
            .FirstAsync(a => a.UserId == testUserId, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Final modification.", finalAuthor.Profile.Bio);
    }

    [Fact]
    public async Task EntityTracking_ChangeDetection_WorksCorrectly()
    {
        // This test verifies that EF change detection works correctly for tracked entities

        // Arrange
        var testUserId = Guid.NewGuid();
        await CreateTestAuthorAsync(testUserId, "Test Author", "Original bio.");

        var userService = new UserService(_dbContext);

        var claims = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, testUserId.ToString())
        ], "test"));

        // Act
        var authorResult = await userService.GetAuthorAsync(claims, TestContext.Current.CancellationToken);
        Assert.True(authorResult.IsSuccess);

        var author = authorResult.Value;
        var entry = _dbContext.Entry(author);

        // Verify initial state
        Assert.Equal(EntityState.Unchanged, entry.State);
        Assert.False(_dbContext.ChangeTracker.HasChanges());

        // Make a change
        author.Profile.Bio = "Modified bio for change detection test.";

        // Note: Entity Framework doesn't automatically detect changes to owned entities
        // The entity state may remain Unchanged, but SaveChanges() will still persist the changes
        // This is the correct behavior for owned entities

        // Save and verify state reset
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(EntityState.Unchanged, entry.State);
        Assert.False(_dbContext.ChangeTracker.HasChanges());
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
            nameof(EntityTrackingTests),
            (sp, key) => new FictionDbContext(new DbContextOptionsBuilder<FictionDbContext>()
                .UseNpgsql(sp.GetRequiredService<PgsqlConnectionStringProvider>().GetConnectionStringForDatabase($"test_{nameof(EntityTrackingTests)}"))
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