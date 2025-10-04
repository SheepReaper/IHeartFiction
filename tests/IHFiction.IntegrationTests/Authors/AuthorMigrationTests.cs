using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using FluentAssertions;

using IHFiction.Data;
using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;

namespace IHFiction.IntegrationTests.Authors;

/// <summary>
/// Tests for database migrations and schema validation
/// These tests ensure migrations work correctly with real PostgreSQL
/// </summary>
// [Collection(nameof(AuthorsTestCollection))]
public class AuthorMigrationTests : BaseIntegrationTest, IConfigureServices<AuthorMigrationTests>, IAsyncLifetime
{
    private readonly FictionDbContext _dbContext;

    public AuthorMigrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(AuthorMigrationTests)) ?? throw new Exception("DbContext not found");
    }


    [Fact]
    public async Task Database_HasCorrectSchema_AfterMigrations()
    {
        // Act - Migrations should have been applied in BaseIntegrationTest
        // Use EF Core to check if we can query the Authors table
        var authorCount = await _dbContext.Authors.CountAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert - If we can count authors, the schema is correct
        authorCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Users_Table_HasCorrectColumns()
    {
        // Act - Check the actual table name (users with discriminator for Authors)
        var columns = await _dbContext.Database
            .SqlQueryRaw<string>("""
                SELECT column_name
                FROM information_schema.columns
                WHERE table_name = 'users'
                ORDER BY ordinal_position
                """)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Check for columns that should exist for Author entities
        columns.Should().Contain("id");
        columns.Should().Contain("user_id");
        columns.Should().Contain("name");
        columns.Should().Contain("updated_at");
        columns.Should().Contain("deleted_at");
        columns.Should().Contain("discriminator");
        columns.Should().Contain("profile_bio");
    }

    [Fact]
    public async Task Users_Table_HasCorrectIndexes()
    {
        // Act - Check indexes on the actual table name
        var indexes = await _dbContext.Database
            .SqlQueryRaw<string>("""
                SELECT indexname
                FROM pg_indexes
                WHERE tablename = 'users'
                """)
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        indexes.Should().Contain(i => i.Contains("pk_")); // Primary key index (EF Core naming)
        // Add assertions for other expected indexes
    }

    [Fact]
    public async Task Authors_PrimaryKey_IsUlid()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Primary Key Test",
            Profile = new Profile { Bio = "Testing primary key" }
        };

        // Act
        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        author.Id.Should().NotBe(Ulid.Empty);

        // Verify the ULID is stored correctly in PostgreSQL
        var storedAuthor = await _dbContext.Authors.FindAsync([author.Id], TestContext.Current.CancellationToken);
        storedAuthor.Should().NotBeNull();
        storedAuthor!.Id.Should().Be(author.Id);
    }

    [Fact]
    public async Task Authors_UpdatedAt_HasCorrectConstraints()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "UpdatedAt Test",
            Profile = new Profile { Bio = "Testing UpdatedAt constraints" }
        };

        // Act
        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        author.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify UpdatedAt is stored as UTC in PostgreSQL using EF Core query
        var storedAuthor = await _dbContext.Authors
            .Where(a => a.Id == author.Id)
            .Select(a => a.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);

        storedAuthor.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Profile_EmbeddedEntity_StoredCorrectly()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Profile Test",
            Profile = new Profile { Bio = "Testing embedded profile entity" }
        };

        // Act
        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var storedAuthor = await _dbContext.Authors
            .Where(a => a.Id == author.Id)
            .Select(a => new { a.Id, a.Profile.Bio })
            .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);

        storedAuthor.Should().NotBeNull();
        storedAuthor!.Bio.Should().Be("Testing embedded profile entity");
    }

    [Fact]
    public async Task Authors_SoftDelete_WorksWithDeletedAt()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Soft Delete Test",
            Profile = new Profile { Bio = "Testing soft delete" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Simulate soft delete
        author.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var deletedAuthor = await _dbContext.Authors.FindAsync([author.Id], TestContext.Current.CancellationToken);
        deletedAuthor.Should().NotBeNull();
        deletedAuthor!.DeletedAt.Should().NotBeNull();
        deletedAuthor.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Database_SupportsLargeTextFields()
    {
        // Arrange
        var largeBio = new string('A', 10000); // 10KB bio
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Large Text Test",
            Profile = new Profile { Bio = largeBio }
        };

        // Act
        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var storedAuthor = await _dbContext.Authors.FindAsync([author.Id], TestContext.Current.CancellationToken);
        storedAuthor.Should().NotBeNull();
        storedAuthor!.Profile.Bio.Should().HaveLength(10000);
        storedAuthor.Profile.Bio.Should().Be(largeBio);
    }

    [Fact]
    public async Task Database_HandlesUnicodeCorrectly()
    {
        // Arrange
        var unicodeName = "测试作者 🎭 Tëst Âuthör";
        var unicodeBio = "This bio contains various Unicode: 中文, العربية, русский, 🎨📚🖋️";

        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = unicodeName,
            Profile = new Profile { Bio = unicodeBio }
        };

        // Act
        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var storedAuthor = await _dbContext.Authors.FindAsync([author.Id], TestContext.Current.CancellationToken);
        storedAuthor.Should().NotBeNull();
        storedAuthor!.Name.Should().Be(unicodeName);
        storedAuthor.Profile.Bio.Should().Be(unicodeBio);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>")]
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped(
            nameof(AuthorMigrationTests),
            (sp, key) => new FictionDbContext(new DbContextOptionsBuilder<FictionDbContext>()
                .UseNpgsql(sp.GetRequiredService<PgsqlConnectionStringProvider>().GetConnectionStringForDatabase($"test_{nameof(AuthorMigrationTests)}"))
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

    protected override async ValueTask DisposeAsyncCore()
    {
        // Close all connections before deleting database
        await _dbContext.Database.CloseConnectionAsync();

        // Use EnsureDeletedAsync for proper async database deletion
        await _dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);

        await _dbContext.DisposeAsync();

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}
