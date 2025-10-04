using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using FluentAssertions;

using IHFiction.Data;
using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Authors;
using IHFiction.FictionApi.Common;

namespace IHFiction.IntegrationTests.Authors;


/// <summary>
/// Edge case and error scenario integration tests for GetAuthorById
/// Tests boundary conditions, error handling, and unusual scenarios with database
/// </summary>
// [Collection(nameof(AuthorsTestCollection))]
public class GetAuthorByIdEdgeCaseTests : BaseIntegrationTest, IConfigureServices<GetAuthorByIdEdgeCaseTests>, IAsyncLifetime
{
    private readonly FictionDbContext _dbContext;
    private readonly GetAuthorById _useCase;

    public GetAuthorByIdEdgeCaseTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(GetAuthorByIdEdgeCaseTests)) ?? throw new Exception("DbContext not found");
        _useCase = new GetAuthorById(_dbContext);
    }

    [Fact]
    public async Task HandleAsync_WithMinimumUlid_HandlesCorrectly()
    {
        // Arrange
        var minUlid = Ulid.MinValue;

        // Act
        var result = await _useCase.HandleAsync(minUlid, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.DomainError.Should().Be(CommonErrors.Author.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WithMaximumUlid_HandlesCorrectly()
    {
        // Arrange
        var maxUlid = Ulid.MaxValue;

        // Act
        var result = await _useCase.HandleAsync(maxUlid, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.DomainError.Should().Be(CommonErrors.Author.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WithVeryLongAuthorName_HandlesCorrectly()
    {
        // Arrange
        var veryLongName = new string('A', 1000); // Very long name
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = veryLongName,
            UpdatedAt = DateTime.UtcNow,
            Profile = new Profile { Bio = "Bio" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.Name.Should().Be(veryLongName);
    }

    [Fact]
    public async Task HandleAsync_WithVeryLongBio_HandlesCorrectly()
    {
        // Arrange
        var veryLongBio = new string('B', 10000); // Very long bio
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Author",
            UpdatedAt = DateTime.UtcNow,
            Profile = new Profile { Bio = veryLongBio }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.Profile.Bio.Should().Be(veryLongBio);
    }

    [Fact]
    public async Task HandleAsync_WithSpecialCharactersInName_HandlesCorrectly()
    {
        // Arrange
        var nameWithSpecialChars = "Author with émojis 🎭 and symbols @#$%^&*()";
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = nameWithSpecialChars,
            UpdatedAt = DateTime.UtcNow,
            Profile = new Profile { Bio = "Bio" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.Name.Should().Be(nameWithSpecialChars);
    }

    [Fact]
    public async Task HandleAsync_WithSpecialCharactersInBio_HandlesCorrectly()
    {
        // Arrange
        var bioWithSpecialChars = "Bio with émojis 📚, newlines\n\r, tabs\t, and symbols <>&\"'";
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Author",
            UpdatedAt = DateTime.UtcNow,
            Profile = new Profile { Bio = bioWithSpecialChars }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.Profile.Bio.Should().Be(bioWithSpecialChars);
    }

    [Fact]
    public async Task HandleAsync_WithAutomaticTimestamp_SetsCurrentTime()
    {
        // Arrange
        var beforeSave = DateTime.UtcNow;
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Test Author",
            Profile = new Profile { Bio = "Test bio" }
            // Note: UpdatedAt is now automatically set by interceptor
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var afterSave = DateTime.UtcNow;

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.UpdatedAt.Should().BeAfter(beforeSave);
        response.UpdatedAt.Should().BeBefore(afterSave);
    }

    [Fact]
    public async Task HandleAsync_WithInterceptorManagedTimestamp_ReflectsActualSaveTime()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Interceptor Test Author",
            Profile = new Profile { Bio = "Interceptor managed timestamp" }
            // Note: UpdatedAt is automatically managed by the interceptor
        };

        var beforeSave = DateTime.UtcNow;
        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var afterSave = DateTime.UtcNow;

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        // Verify the timestamp is within the expected range (set by interceptor)
        response.UpdatedAt.Should().BeOnOrAfter(beforeSave);
        response.UpdatedAt.Should().BeOnOrBefore(afterSave);
    }

    [Fact]
    public async Task HandleAsync_WithDeletedAtBeforeUpdatedAt_ReturnsNull()
    {
        // Arrange
        var updatedAt = DateTime.UtcNow;
        var deletedAt = updatedAt.AddDays(-1); // Deleted before last update (unusual but possible)
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Inconsistent Author",
            UpdatedAt = updatedAt,
            DeletedAt = deletedAt,
            Profile = new Profile { Bio = "Bio" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.DomainError.Should().Be(CommonErrors.Author.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WithLargeNumberOfWorks_HandlesCorrectly()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Prolific Author",
            UpdatedAt = DateTime.UtcNow,
            Profile = new Profile { Bio = "Very productive" }
        };

        // Create a large number of works (reduced for performance)
        for (int i = 0; i < 100; i++)
        {
            var work = new Story
            {
                Title = $"Work {i:D3}",
                Description = $"Description for work {i:D3}",
                Owner = author,
                UpdatedAt = DateTime.UtcNow,
                PublishedAt = DateTime.UtcNow.AddDays(-i) // Ensure they are published
            };
            author.Works.Add(work);
        }

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        var works = response.PublishedStories;
        works.Should().HaveCount(100);
        works.Should().Contain(w => w.Title == "Work 000");
        works.Should().Contain(w => w.Title == "Work 099");
    }

    [Fact]
    public async Task HandleAsync_WithWorksHavingSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Special Author",
            UpdatedAt = DateTime.UtcNow,
            Profile = new Profile { Bio = "Bio" }
        };

        var workTitles = new[]
        {
            "Work with émojis 📖",
            "Work with symbols @#$%",
            "Work with quotes \"'",
            "Work with newlines\n\r",
            "Work with tabs\t\t"
        };

        foreach (var title in workTitles)
        {
            var work = new Story
            {
                Title = title,
                Description = $"Description for {title}",
                Owner = author,
                UpdatedAt = DateTime.UtcNow,
                PublishedAt = DateTime.UtcNow.AddDays(-1) // Ensure they are published
            };
            author.Works.Add(work);
        }

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        var works2 = response.PublishedStories;
        works2.Should().HaveCount(5);
        works2.Should().Contain(w => w.Title == "Work with émojis 📖");
        works2.Should().Contain(w => w.Title == "Work with symbols @#$%");
    }

    [Fact]
    public async Task HandleAsync_WithEmptyGuid_HandlesCorrectly()
    {
        // Arrange
        var emptyGuid = Guid.Empty;
        var author = new Author
        {
            UserId = emptyGuid,
            Name = "Author with Empty GUID",
            UpdatedAt = DateTime.UtcNow,
            Profile = new Profile { Bio = "Bio" }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.UserId.Should().Be(emptyGuid);
    }

    [Fact]
    public async Task HandleAsync_WithWhitespaceOnlyBio_HandlesCorrectly()
    {
        // Arrange
        var whitespaceOnlyBio = "   \t\n\r   ";
        var author = new Author
        {
            UserId = Guid.NewGuid(),
            Name = "Author",
            UpdatedAt = DateTime.UtcNow,
            Profile = new Profile { Bio = whitespaceOnlyBio }
        };

        _dbContext.Authors.Add(author);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _useCase.HandleAsync(author.Id, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        var response = result.Value!;
        response.Profile.Bio.Should().Be(whitespaceOnlyBio);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>")]
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped(
            nameof(GetAuthorByIdEdgeCaseTests),
            (sp, key) => new FictionDbContext(new DbContextOptionsBuilder<FictionDbContext>()
                .UseNpgsql(sp.GetRequiredService<PgsqlConnectionStringProvider>().GetConnectionStringForDatabase($"test_{nameof(GetAuthorByIdEdgeCaseTests)}"))
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