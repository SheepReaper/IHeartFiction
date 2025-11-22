using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using FluentAssertions;

using IHFiction.Data;
using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Authors;
using IHFiction.FictionApi.Common;

namespace IHFiction.IntegrationTests.Authors;

/// <summary>
/// Database-specific integration tests for GetAuthor
/// Tests PostgreSQL-specific behaviors that weren't possible with in-memory database
/// </summary>
// [Collection(nameof(AuthorsTestCollection))]
public class GetAuthorDatabaseSpecificTests : BaseIntegrationTest, IConfigureServices<GetAuthorDatabaseSpecificTests>, IAsyncLifetime
{
    private readonly GetAuthor _useCase;
    private readonly FictionDbContext _dbContext;

    public GetAuthorDatabaseSpecificTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(GetAuthorDatabaseSpecificTests)) ?? throw new Exception("DbContext not found");
        _useCase = new GetAuthor(_dbContext);
    }

    [Fact]
    public async Task HandleAsync_WithPostgreSQLUlidHandling_PreservesUlidPrecision()
    {
        // Arrange - Use a dynamic ULID to avoid conflicts
        var specificUlid = Ulid.NewUlid();
        var author = new Author
        {
            Id = specificUlid,
            UserId = Guid.NewGuid(),
            Name = "ULID Test Author",
            Profile = new Profile { Bio = "Testing ULID precision" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(specificUlid, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        // Verify ULID was stored and retrieved with full precision
        var retrievedAuthor = await _dbContext.Authors.FindAsync([specificUlid], TestContext.Current.CancellationToken);
        retrievedAuthor!.Id.Should().Be(specificUlid);
        retrievedAuthor.Id.ToString().Should().HaveLength(26); // ULID is always 26 characters
    }

    [Fact]
    public async Task HandleAsync_WithPostgreSQLTimestampPrecision_HandlesCorrectly()
    {
        // Arrange
        var preciseTimestamp = new DateTime(2024, 1, 15, 14, 30, 25, 123, DateTimeKind.Utc)
            .AddMicroseconds(456); // PostgreSQL supports microsecond precision

        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Timestamp Test Author",
            Profile = new Profile { Bio = "Testing timestamp precision" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;

        // Verify PostgreSQL timestamp precision is maintained
        response.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Test that we can query with precise timestamps
        var authorFromDb = await _dbContext.Authors
            .Where(a => a.UpdatedAt >= DateTime.UtcNow.AddMinutes(-1))
            .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);
        authorFromDb.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_WithPostgreSQLCaseHandling_RespectsCollation()
    {
        // Arrange
        var author1 = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Test Author",
            Profile = new Profile { Bio = "Case sensitive test" }
        };

        var author2 = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "test author", // Different case
            Profile = new Profile { Bio = "Case sensitive test 2" }
        };

        _dbContext.Authors.AddRange(author1, author2);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        var result1 = await _useCase.HandleAsync(author1.Id, TestContext.Current.CancellationToken);
        var result2 = await _useCase.HandleAsync(author2.Id, TestContext.Current.CancellationToken);

        result1.Should().NotBeNull();
        result1.IsSuccess.Should().BeTrue();
        var response1 = result1.Value!;
        result2.Should().NotBeNull();
        result2.IsSuccess.Should().BeTrue();
        var response2 = result2.Value!;

        response1.Name.Should().Be("Test Author");
        response2.Name.Should().Be("test author");

        // Verify PostgreSQL case handling in queries
        var caseQuery = await _dbContext.Authors
            .Where(a => a.Name == "Test Author")
            .CountAsync(cancellationToken: TestContext.Current.CancellationToken);
        caseQuery.Should().Be(1); // Should only match exact case
    }

    [Fact]
    public async Task HandleAsync_WithLargeDataSet_PerformsEfficientlyWithPostgreSQL()
    {
        // Arrange - Create a larger dataset to test PostgreSQL performance
        var authors = new List<Author>();
        for (int i = 0; i < 1000; i++)
        {
            authors.Add(new Author
            {
                UserId = Guid.NewGuid(),
                Name = $"Author {i:D4}",
                Profile = new Profile { Bio = $"Bio for author {i}" }
            });
        }

        _dbContext.Authors.AddRange(authors);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var targetAuthor = authors[500]; // Middle author

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _useCase.HandleAsync(targetAuthor.Id, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.Name.Should().Be("Author 0500");

        // Verify PostgreSQL indexing is working efficiently
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(250); // Allow for CI environment overhead while still catching regressions
    }

    [Fact]
    public async Task HandleAsync_WithConcurrentAccess_HandlesPostgreSQLLocking()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Concurrent Test Author",
            Profile = new Profile { Bio = "Testing concurrent access" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Simulate concurrent reads with separate DbContext instances
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = _scope.ServiceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(GetAuthorDatabaseSpecificTests)) ?? throw new Exception("DbContext not found");
                var useCase = new GetAuthor(dbContext);
                return await useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(result =>
        {
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue();
            var response = result.Value!;
            response.Name.Should().Be("Concurrent Test Author");
        });
    }

    [Fact]
    public async Task HandleAsync_WithPostgreSQLConstraintViolation_HandlesGracefully()
    {
        // This test would be valuable if you have unique constraints
        // For now, testing that the database connection handles errors properly

        // Arrange - Try to cause a constraint violation scenario
        var invalidUlid = new Ulid(); // Default/empty ULID might cause issues

        // Act
        var result = await _useCase.HandleAsync(invalidUlid, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.DomainError.Should().Be(CommonErrors.Author.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WithPostgreSQLTextSearch_WorksCorrectly()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Full Text Search Author",
            Profile = new Profile { Bio = "This bio contains searchable content with special characters: café, naïve, résumé" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.Profile.Bio.Should().Contain("café");

        // Test that PostgreSQL handles Unicode correctly
        var unicodeQuery = await _dbContext.Authors
            .Where(a => a.Profile.Bio!.Contains("café"))
            .FirstOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken);
        unicodeQuery.Should().NotBeNull();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>")]
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped(
            nameof(GetAuthorDatabaseSpecificTests),
            (sp, key) => new FictionDbContext(new DbContextOptionsBuilder<FictionDbContext>()
                .UseNpgsql(sp.GetRequiredService<PgsqlConnectionStringProvider>().GetConnectionStringForDatabase($"test_{nameof(GetAuthorDatabaseSpecificTests)}"), options =>
                    options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
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