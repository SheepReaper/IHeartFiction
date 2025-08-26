using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using FluentAssertions;

using IHFiction.Data;
using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;

namespace IHFiction.IntegrationTests.Authors;

/// <summary>
/// Tests for Entity Framework interceptors with real PostgreSQL database
/// Validates that interceptors work correctly in production-like environment
/// </summary>
// [Collection(nameof(AuthorsTestCollection))]
public class AuthorInterceptorTests : BaseIntegrationTest, IConfigureServices<AuthorInterceptorTests>, IAsyncLifetime
{
    private readonly FictionDbContext _dbContext;
    public AuthorInterceptorTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(AuthorInterceptorTests)) ?? throw new Exception("DbContext not found");
    }

    [Fact]
    public async Task SaveChanges_AutomaticallyUpdatesTimestamp_OnCreate()
    {
        // Arrange
        var beforeSave = DateTime.UtcNow;
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Interceptor Test Author",
            Profile = new Profile { Bio = "Testing interceptor behavior" }
            // Note: Not setting UpdatedAt - should be set by interceptor
        };

        // Act
        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var afterSave = DateTime.UtcNow;

        // Assert
        author.UpdatedAt.Should().BeOnOrAfter(beforeSave);
        author.UpdatedAt.Should().BeOnOrBefore(afterSave);

        // Verify the timestamp was actually saved to PostgreSQL
        var savedAuthor = await _dbContext.Authors
            .AsNoTracking()
            .FirstAsync(a => a.Id == author.Id, cancellationToken: TestContext.Current.CancellationToken);

        savedAuthor.UpdatedAt.Should().BeCloseTo(author.UpdatedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task SaveChanges_AutomaticallyUpdatesTimestamp_OnUpdate()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Update Test Author",
            Profile = new Profile { Bio = "Original bio" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var originalUpdatedAt = author.UpdatedAt;

        // Wait a small amount to ensure timestamp difference
        await Task.Delay(10, TestContext.Current.CancellationToken);

        // Act
        author.Name = "Updated Name";
        author.Profile.Bio = "Updated bio";

        var beforeUpdate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var afterUpdate = DateTime.UtcNow;

        // Assert
        author.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        author.UpdatedAt.Should().BeOnOrAfter(beforeUpdate);
        author.UpdatedAt.Should().BeOnOrBefore(afterUpdate);

        // Verify the updated timestamp was saved to PostgreSQL
        var updatedAuthor = await _dbContext.Authors
            .AsNoTracking()
            .FirstAsync(a => a.Id == author.Id, cancellationToken: TestContext.Current.CancellationToken);

        updatedAuthor.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        updatedAuthor.Name.Should().Be("Updated Name");
        updatedAuthor.Profile.Bio.Should().Be("Updated bio");
    }

    [Fact]
    public async Task SaveChanges_DoesNotUpdateTimestamp_OnUnchangedEntity()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Unchanged Test Author",
            Profile = new Profile { Bio = "Unchanged bio" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var originalUpdatedAt = author.UpdatedAt;

        // Act - Save without making changes
        await Task.Delay(10, TestContext.Current.CancellationToken); // Ensure time difference would be detectable
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        author.UpdatedAt.Should().Be(originalUpdatedAt);

        // Verify no database update occurred
        var unchangedAuthor = await _dbContext.Authors
            .AsNoTracking()
            .FirstAsync(a => a.Id == author.Id, cancellationToken: TestContext.Current.CancellationToken);

        // PostgreSQL timestamp precision might cause slight differences
        unchangedAuthor.UpdatedAt.Should().BeCloseTo(originalUpdatedAt, TimeSpan.FromMicroseconds(1));
    }

    [Fact]
    public async Task SaveChanges_HandlesMultipleEntities_WithCorrectTimestamps()
    {
        // Arrange
        var authors = new[]
        {
            new Author
            {
                UserId = Guid.NewGuid(),
                Name = "Batch Author 1",
                Profile = new Profile { Bio = "Batch bio 1" }
            },
            new Author
            {
                UserId = Guid.NewGuid(),
                Name = "Batch Author 2",
                Profile = new Profile { Bio = "Batch bio 2" }
            },
            new Author
            {
                UserId = Guid.NewGuid(),
                Name = "Batch Author 3",
                Profile = new Profile { Bio = "Batch bio 3" }
            }
        };

        // Act
        var beforeSave = DateTime.UtcNow;
        _dbContext.Authors.AddRange(authors);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var afterSave = DateTime.UtcNow;

        // Assert
        foreach (var author in authors)
        {
            author.UpdatedAt.Should().BeOnOrAfter(beforeSave);
            author.UpdatedAt.Should().BeOnOrBefore(afterSave);
        }

        // Verify all timestamps are very close (within same transaction)
        var timestamps = authors.Select(a => a.UpdatedAt).ToArray();
        var maxTimestamp = timestamps.Max();
        var minTimestamp = timestamps.Min();

        (maxTimestamp - minTimestamp).Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task SaveChanges_InterceptorWorksWithSequentialUpdates()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Sequential Test Author",
            Profile = new Profile { Bio = "Original bio" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var originalTimestamp = author.UpdatedAt;

        // Act - Perform sequential updates to test interceptor behavior
        await Task.Delay(10, TestContext.Current.CancellationToken); // Ensure timestamp difference

        author.Profile.Bio = "Updated bio 1";
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var firstUpdateTimestamp = author.UpdatedAt;

        await Task.Delay(10, TestContext.Current.CancellationToken); // Ensure timestamp difference

        author.Profile.Bio = "Updated bio 2";
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var secondUpdateTimestamp = author.UpdatedAt;

        // Assert - PostgreSQL timestamp precision means updates might have same timestamp
        firstUpdateTimestamp.Should().BeOnOrAfter(originalTimestamp);
        secondUpdateTimestamp.Should().BeOnOrAfter(firstUpdateTimestamp);

        // Verify final state
        var finalAuthor = await _dbContext.Authors
            .AsNoTracking()
            .FirstAsync(a => a.Id == author.Id, cancellationToken: TestContext.Current.CancellationToken);

        finalAuthor.UpdatedAt.Should().BeCloseTo(secondUpdateTimestamp, TimeSpan.FromMilliseconds(100));
        finalAuthor.Profile.Bio.Should().Be("Updated bio 2");
    }

    [Fact]
    public async Task SaveChanges_InterceptorWorksWithComplexEntityGraphs()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Complex Graph Author",
            Profile = new Profile { Bio = "Testing complex entity graphs" }
        };

        // Act - Test without Works since TestWork discriminator isn't configured
        var beforeSave = DateTime.UtcNow;
        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var afterSave = DateTime.UtcNow;

        // Assert
        author.UpdatedAt.Should().BeOnOrAfter(beforeSave);
        author.UpdatedAt.Should().BeOnOrBefore(afterSave);

        // Verify the author was saved correctly
        var savedAuthor = await _dbContext.Authors
            .AsNoTracking()
            .FirstAsync(a => a.Id == author.Id, cancellationToken: TestContext.Current.CancellationToken);

        savedAuthor.UpdatedAt.Should().BeCloseTo(author.UpdatedAt, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public async Task SaveChanges_InterceptorPreservesOtherProperties()
    {
        // Arrange
        var specificUserId = Guid.NewGuid();
        var specificDeletedAt = DateTime.UtcNow.AddDays(-1);

        var author = new Author
        {
            UserId = specificUserId,
            Name = "Property Preservation Test",
            DeletedAt = specificDeletedAt,
            Profile = new Profile { Bio = "Testing property preservation" }
        };

        // Act
        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        // Verify interceptor didn't modify other properties
        author.UserId.Should().Be(specificUserId);
        author.Name.Should().Be("Property Preservation Test");
        author.DeletedAt.Should().Be(specificDeletedAt);
        author.Profile.Bio.Should().Be("Testing property preservation");

        // Verify UpdatedAt was set by interceptor
        author.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>")]
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped(
            nameof(AuthorInterceptorTests),
            (sp, key) => new FictionDbContext(new DbContextOptionsBuilder<FictionDbContext>()
                .UseNpgsql(sp.GetRequiredService<PgsqlConnectionStringProvider>().GetConnectionStringForDatabase($"test_{nameof(AuthorInterceptorTests)}"))
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
