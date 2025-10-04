using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Authors;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Stories;

using MongoDB.Bson;
using MongoDB.Driver;

namespace IHFiction.IntegrationTests.Stories;

// [Collection(nameof(StoriesTestCollection))]
public sealed class ConvertStoryTypeIntegrationTests : BaseIntegrationTest, IConfigureServices<ConvertStoryTypeIntegrationTests>, IAsyncLifetime
{
    private readonly FictionDbContext _dbContext;
    private readonly StoryDbContext _storyDbContext;
    private readonly EntityLoaderService _entityLoader;
    private readonly ConvertStoryType _useCase;

    public ConvertStoryTypeIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _dbContext = _scope.ServiceProvider.GetKeyedService<FictionDbContext>(nameof(ConvertStoryTypeIntegrationTests)) ?? throw new Exception("DbContext not found");
        _storyDbContext = _scope.ServiceProvider.GetKeyedService<StoryDbContext>(nameof(ConvertStoryTypeIntegrationTests)) ?? throw new Exception("StoryDbContext not found");

        _entityLoader = new EntityLoaderService(_dbContext);

        // Construct a test-local UserService + AuthorizationService to avoid DI validation that expects global app connection strings
        var userService = new UserService(_dbContext);
        var authorization = new AuthorizationService(_dbContext, userService);

        _useCase = new ConvertStoryType(_dbContext, _storyDbContext, _entityLoader, authorization);
    }

    [Fact]
    public async Task UpgradeOneShotToChaptered_PersistsRelationalAndStoryDbChanges()
    {
        // Arrange: create author and story with WorkBodyId and ensure StoryDb has the WorkBody
        var author = new Data.Authors.Domain.Author { Name = "Integration Author", UserId = Guid.NewGuid() };

        var workBodyId = ObjectId.GenerateNewId();

        var story = new Story
        {
            Title = "Integration Test Story",
            Description = "Test",
            Owner = author,
            OwnerId = author.Id,
            WorkBodyId = workBodyId
        };

        _dbContext.Authors.Add(author);
        _dbContext.Stories.Add(story);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Add WorkBody into story DB
        _storyDbContext.WorkBodies.Add(new WorkBody { Id = workBodyId, Content = "Original content" });
        await _storyDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Build a ClaimsPrincipal for the author so authorization resolves
        var claims = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, author.UserId.ToString())
        ]));

        var body = new ConvertStoryType.ConvertStoryTypeBody(TargetType: StoryType.MultiChapter);

        // Act
        var result = await _useCase.HandleAsync(story.Id, body, claims, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess);

        // Reload from relational DB
        var persistedStory = await _dbContext.Stories.Include(s => s.Chapters).FirstAsync(s => s.Id == story.Id, TestContext.Current.CancellationToken);

        Assert.Null(persistedStory.WorkBodyId);
        Assert.Single(persistedStory.Chapters);

        var chapter = persistedStory.Chapters[0];
        Assert.Equal(workBodyId, chapter.WorkBodyId);

        // Verify story DB still contains the work body
        var wb = await _storyDbContext.WorkBodies.FirstOrDefaultAsync(w => w.Id == workBodyId, TestContext.Current.CancellationToken);
        Assert.NotNull(wb);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>")]
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedScoped(
            nameof(ConvertStoryTypeIntegrationTests),
            (sp, key) => new FictionDbContext(new DbContextOptionsBuilder<FictionDbContext>()
                .UseNpgsql(sp.GetRequiredService<PgsqlConnectionStringProvider>().GetConnectionStringForDatabase($"test_{nameof(ConvertStoryTypeIntegrationTests)}"))
                .UseSnakeCaseNamingConvention()
                .Options));

        services.AddKeyedScoped(
            nameof(ConvertStoryTypeIntegrationTests),
            (sp, key) => new StoryDbContext(new DbContextOptionsBuilder<StoryDbContext>()
                .UseMongoDB(sp.GetRequiredService<IMongoClient>(), $"test_stories_{nameof(ConvertStoryTypeIntegrationTests)}")
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
        // Close connections and drop databases
        await _dbContext.Database.CloseConnectionAsync();
        await _dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await _storyDbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}
