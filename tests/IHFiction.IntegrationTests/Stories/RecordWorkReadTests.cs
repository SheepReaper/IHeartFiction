using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Stories;

using MongoDB.Bson;

namespace IHFiction.IntegrationTests.Stories;

public sealed class RecordWorkReadTests : BaseIntegrationTest, IConfigureServices<RecordWorkReadTests>, IAsyncLifetime
{
    private readonly FictionDbContext context;
    private readonly RecordWorkReadHandler handler;
    private readonly IntegrationTestWebAppFactory factory;
    private bool disposed;

    public RecordWorkReadTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
        context = _scope.ServiceProvider.GetRequiredKeyedService<FictionDbContext>(nameof(RecordWorkReadTests));
        handler = new RecordWorkReadHandler(context, TimeProvider.System);
    }

    [Fact]
    public async Task DifferentReadersRecordingConcurrently_AtomicallyIncrementCount()
    {
        var author = new Author { Name = "Concurrent author", UserId = Guid.NewGuid() };
        var story = new Story
        {
            Title = "Concurrent one-shot",
            Description = "Description",
            Owner = author,
            WorkBodyId = ObjectId.GenerateNewId(),
            PublishedAt = DateTime.UtcNow
        };
        story.Authors.Add(author);
        context.AddRange(author, story);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredKeyedService<FictionDbContext>(nameof(RecordWorkReadTests));
        var secondContext = secondScope.ServiceProvider.GetRequiredKeyedService<FictionDbContext>(nameof(RecordWorkReadTests));
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var body = new RecordWorkRead.RecordWorkReadBody(10, true);

        await Task.WhenAll(
            new RecordWorkReadHandler(firstContext, TimeProvider.System).Handle(
                new RecordWorkReadRequested(story.Id, null, "device-one"), TestContext.Current.CancellationToken),
            new RecordWorkReadHandler(secondContext, TimeProvider.System).Handle(
                new RecordWorkReadRequested(story.Id, null, "device-two"), TestContext.Current.CancellationToken));
        context.ChangeTracker.Clear();

        Assert.Equal(2, (await context.Stories.SingleAsync(candidate => candidate.Id == story.Id, TestContext.Current.CancellationToken)).ReadCount);
    }

    [Fact]
    public async Task RepeatedDeliveryForSameReader_CountsOnlyOnce()
    {
        var author = new Author { Name = "Retry author", UserId = Guid.NewGuid() };
        var story = new Story
        {
            Title = "Retry-safe story",
            Description = "Description",
            Owner = author,
            WorkBodyId = ObjectId.GenerateNewId(),
            PublishedAt = DateTime.UtcNow
        };
        story.Authors.Add(author);
        context.AddRange(author, story);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var message = new RecordWorkReadRequested(story.Id, null, "retry-device");

        await handler.Handle(message, TestContext.Current.CancellationToken);
        await handler.Handle(message, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        Assert.Equal(1, await context.WorkReads.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, (await context.Stories.SingleAsync(
            candidate => candidate.Id == story.Id,
            TestContext.Current.CancellationToken)).ReadCount);
    }

    [Fact]
    public async Task SameAnonymousReaderAcrossTwoChapters_CountsOnceForStoryAndOncePerChapter()
    {
        var author = new Author { Name = "Read count author", UserId = Guid.NewGuid() };
        var story = new Story
        {
            Title = "A chaptered story",
            Description = "Description",
            Owner = author,
            PublishedAt = DateTime.UtcNow
        };
        story.Authors.Add(author);
        var first = NewChapter(story, author, "First", 1);
        var second = NewChapter(story, author, "Second", 2);
        context.AddRange(author, story, first, second);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await handler.Handle(new RecordWorkReadRequested(first.Id, null, "same-device"), TestContext.Current.CancellationToken);
        await handler.Handle(new RecordWorkReadRequested(second.Id, null, "same-device"), TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var persistedStory = await context.Stories.SingleAsync(candidate => candidate.Id == story.Id, TestContext.Current.CancellationToken);
        var persistedChapters = await context.Chapters.Where(candidate => candidate.StoryId == story.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, persistedStory.ReadCount);
        Assert.All(persistedChapters, chapter => Assert.Equal(1, chapter.ReadCount));
        Assert.Equal(3, await context.WorkReads.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StoryAuthorRead_TracksHistoryWithoutIncreasingPublicCount()
    {
        var author = new Author { Name = "Story owner", UserId = Guid.NewGuid() };
        var story = new Story
        {
            Title = "An owned one-shot",
            Description = "Description",
            Owner = author,
            WorkBodyId = ObjectId.GenerateNewId(),
            PublishedAt = DateTime.UtcNow
        };
        story.Authors.Add(author);
        context.AddRange(author, story);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, author.UserId.ToString())], "test"));

        await handler.Handle(
            new RecordWorkReadRequested(story.Id, author.UserId, "author-device"),
            TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        Assert.Equal(0, (await context.Stories.SingleAsync(candidate => candidate.Id == story.Id, TestContext.Current.CancellationToken)).ReadCount);
        Assert.False((await context.WorkReads.SingleAsync(TestContext.Current.CancellationToken)).IsCounted);
    }

    [Fact]
    public async Task SoftDeletedWork_HidesItsReadsUnlessQueryFiltersAreIgnored()
    {
        var author = new Author { Name = "Deleted story author", UserId = Guid.NewGuid() };
        var story = new Story
        {
            Title = "Deleted story",
            Description = "Description",
            Owner = author,
            WorkBodyId = ObjectId.GenerateNewId(),
            PublishedAt = DateTime.UtcNow
        };
        story.Authors.Add(author);
        context.AddRange(author, story);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await handler.Handle(
            new RecordWorkReadRequested(story.Id, null, "deleted-story-reader"),
            TestContext.Current.CancellationToken);

        story.DeletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        Assert.Empty(await context.WorkReads.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.WorkReads
            .IgnoreQueryFilters()
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    private static Chapter NewChapter(Story story, Author author, string title, int order) => new()
    {
        Title = title,
        Order = order,
        Owner = author,
        Story = story,
        WorkBodyId = ObjectId.GenerateNewId(),
        PublishedAt = DateTime.UtcNow
    };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>")]
    public static void ConfigureServices(IServiceCollection services) => services
        .AddKeyedTestFictionDbContext<RecordWorkReadTests>(configurePendingModelWarning: false, useDefaultInterceptors: false);

    public async ValueTask InitializeAsync()
    {
        if (context.Database.GetPendingMigrations().Any())
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        if (disposed) return;
        await context.Database.CloseConnectionAsync();
        await context.Database.EnsureDeletedAsync();
        await context.DisposeAsync();
        disposed = true;
        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}
