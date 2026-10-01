using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.FictionApi.Stories;
using IHFiction.FictionApi.Tags;

using NSubstitute;

using Wolverine;

namespace IHFiction.IntegrationTests.Stories;

public sealed class TagTaxonomyIntegrationTests : BaseIntegrationTest, IConfigureServices<TagTaxonomyIntegrationTests>, IAsyncLifetime
{
    private readonly FictionDbContext _context;
    private bool _disposed;

    public TagTaxonomyIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        _context = _scope.ServiceProvider.GetRequiredKeyedService<FictionDbContext>(nameof(TagTaxonomyIntegrationTests));
    }

    [Fact]
    public async Task ReplaceStoryTags_AddsAndRemovesTheCompleteSet()
    {
        var author = new Data.Authors.Domain.Author { Name = "Tag Author", UserId = Guid.NewGuid() };
        var story = new Story { Title = "Tagged Story", Description = "Test", Owner = author, OwnerId = author.Id };
        _context.AddRange(author, story);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new AddTagsToStory(_context, new UserService(_context), Substitute.For<IMessageBus>());
        var principal = Principal(author.UserId);

        var first = await useCase.HandleAsync(
            story.Id,
            new AddTagsToStory.AddTagsToStoryBody(["genre:Fantasy", "theme:Found Family"]),
            principal,
            TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.Equal(2, first.Value.TotalTags);

        var replacement = await useCase.HandleAsync(
            story.Id,
            new AddTagsToStory.AddTagsToStoryBody(["genre:Fantasy"]),
            principal,
            TestContext.Current.CancellationToken);

        Assert.True(replacement.IsSuccess);
        Assert.Single(replacement.Value.Tags);
        Assert.Single((await _context.Stories.Include(candidate => candidate.Tags)
            .SingleAsync(candidate => candidate.Id == story.Id, TestContext.Current.CancellationToken)).Tags);

        var empty = await useCase.HandleAsync(
            story.Id,
            new AddTagsToStory.AddTagsToStoryBody([]),
            principal,
            TestContext.Current.CancellationToken);

        Assert.True(empty.IsSuccess);
        Assert.Equal(0, empty.Value.TotalTags);
    }

    [Fact]
    public async Task ReplaceStoryTags_SetsOneHundredTagsInOneRequest()
    {
        var author = new Data.Authors.Domain.Author { Name = "Bulk Tag Author", UserId = Guid.NewGuid() };
        var story = new Story { Title = "Bulk Tagged Story", Description = "Test", Owner = author, OwnerId = author.Id };
        _context.AddRange(author, story);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var tags = Enumerable.Range(1, 100).Select(index => $"theme:Group {index % 5}:Value {index:D3}").ToArray();
        var useCase = new AddTagsToStory(_context, new UserService(_context), Substitute.For<IMessageBus>());

        var result = await useCase.HandleAsync(
            story.Id,
            new AddTagsToStory.AddTagsToStoryBody(tags),
            Principal(author.UserId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.TotalTags);
        Assert.Equal(100, await _context.Stories
            .Where(candidate => candidate.Id == story.Id)
            .SelectMany(candidate => candidate.Tags)
            .CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublicTagDiscovery_CountsPublishedStoriesAndFiltersByCanonicalOrSynonymKey()
    {
        var author = new Data.Authors.Domain.Author { Name = "Discovery Author", UserId = Guid.NewGuid() };
        var canonicalTag = Tag.CreateCanonical("universe", null, "Kantai Collection");
        var synonym = Tag.CreateSynonym(canonicalTag, "universe", null, "Kancolle");
        var unrelatedTag = Tag.CreateCanonical("universe", null, "Ace Combat");
        var publishedMatch = new Story
        {
            Title = "Published Match",
            Description = "Discoverable by tag.",
            Owner = author,
            OwnerId = author.Id,
            PublishedAt = DateTime.UtcNow
        };
        var draftMatch = new Story
        {
            Title = "Draft Match",
            Description = "Must stay private.",
            Owner = author,
            OwnerId = author.Id
        };
        var publishedOther = new Story
        {
            Title = "Published Other",
            Description = "A different universe.",
            Owner = author,
            OwnerId = author.Id,
            PublishedAt = DateTime.UtcNow
        };
        publishedMatch.Tags.Add(canonicalTag);
        draftMatch.Tags.Add(canonicalTag);
        publishedOther.Tags.Add(unrelatedTag);
        _context.AddRange(author, canonicalTag, synonym, unrelatedTag, publishedMatch, draftMatch, publishedOther);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();

        var paginator = new PaginationService(Options.Create(new PaginationOptions()));
        var listTags = new ListTags(_context, paginator);
        var tagsResult = await listTags.HandleAsync(
            new ListTags.ListTagsQuery(PageSize: 200),
            new ListTags.ListTagsBody(),
            TestContext.Current.CancellationToken);

        Assert.True(tagsResult.IsSuccess);
        var listedTag = Assert.Single(tagsResult.Value.Data, tag => tag.TagId == canonicalTag.Id);
        Assert.Equal("universe:kantaicollection", listedTag.NormalizedKey);
        Assert.Equal(1, listedTag.StoryCount);

        var listStories = new ListPublishedStories(_context, paginator);
        var canonicalResult = await listStories.HandleAsync(
            new ListPublishedStories.ListPublishedStoriesQuery(TagKey: canonicalTag.NormalizedKey),
            TestContext.Current.CancellationToken);
        var synonymResult = await listStories.HandleAsync(
            new ListPublishedStories.ListPublishedStoriesQuery(TagKey: synonym.NormalizedKey.ToUpperInvariant()),
            TestContext.Current.CancellationToken);
        var unknownResult = await listStories.HandleAsync(
            new ListPublishedStories.ListPublishedStoriesQuery(TagKey: "universe:unknown"),
            TestContext.Current.CancellationToken);

        Assert.True(canonicalResult.IsSuccess);
        Assert.True(synonymResult.IsSuccess);
        Assert.True(unknownResult.IsSuccess);
        Assert.Equal(publishedMatch.Id, Assert.Single(canonicalResult.Value!.Data).StoryId);
        Assert.Equal(publishedMatch.Id, Assert.Single(synonymResult.Value!.Data).StoryId);
        Assert.Empty(unknownResult.Value!.Data);
    }

    [Fact]
    public async Task PublicTagDiscovery_SupportsValueOnlyAndMultiTagIntersection()
    {
        var author = new Data.Authors.Domain.Author { Name = "Tag Discovery Author", UserId = Guid.NewGuid() };
        var fantasyTag = Tag.CreateCanonical("genre", null, "Fantasy");
        var kancolleTag = Tag.CreateCanonical("universe", null, "Kantai Collection");
        var kancolleSynonym = Tag.CreateSynonym(kancolleTag, "universe", null, "Kancolle");
        var sciFiTag = Tag.CreateCanonical("genre", null, "Sci-Fi");

        var fantasyKancolleStory = new Story
        {
            Title = "Fantasy Kancolle",
            Description = "A cross-genre story.",
            Owner = author,
            OwnerId = author.Id,
            PublishedAt = DateTime.UtcNow
        };
        fantasyKancolleStory.Tags.Add(fantasyTag);
        fantasyKancolleStory.Tags.Add(kancolleTag);

        var fantasyOnlyStory = new Story
        {
            Title = "Fantasy Only",
            Description = "Pure fantasy.",
            Owner = author,
            OwnerId = author.Id,
            PublishedAt = DateTime.UtcNow
        };
        fantasyOnlyStory.Tags.Add(fantasyTag);

        _context.AddRange(author, fantasyTag, kancolleTag, kancolleSynonym, sciFiTag, fantasyKancolleStory, fantasyOnlyStory);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();

        var paginator = new PaginationService(Options.Create(new PaginationOptions()));
        var listStories = new ListPublishedStories(_context, paginator);

        // Value-only query matching canonical tag
        var fantasyResult = await listStories.HandleAsync(
            new ListPublishedStories.ListPublishedStoriesQuery(TagKey: "fantasy"),
            TestContext.Current.CancellationToken);
        Assert.True(fantasyResult.IsSuccess);
        Assert.Equal(2, fantasyResult.Value!.Data.Count());

        // Value-only query matching synonym tag
        var synonymValueResult = await listStories.HandleAsync(
            new ListPublishedStories.ListPublishedStoriesQuery(TagKey: "kancolle"),
            TestContext.Current.CancellationToken);
        Assert.True(synonymValueResult.IsSuccess);
        Assert.Single(synonymValueResult.Value!.Data);
        Assert.Equal(fantasyKancolleStory.Id, synonymValueResult.Value!.Data.First().StoryId);

        // Multi-tag intersection query (canonical + synonym)
        var intersectionResult = await listStories.HandleAsync(
            new ListPublishedStories.ListPublishedStoriesQuery(TagKey: "fantasy+kancolle"),
            TestContext.Current.CancellationToken);
        Assert.True(intersectionResult.IsSuccess);
        Assert.Single(intersectionResult.Value!.Data);
        Assert.Equal(fantasyKancolleStory.Id, intersectionResult.Value!.Data.First().StoryId);

        // Multi-tag intersection query (fully-qualified)
        var fullyQualifiedIntersection = await listStories.HandleAsync(
            new ListPublishedStories.ListPublishedStoriesQuery(TagKey: "genre:fantasy+universe:kantaicollection"),
            TestContext.Current.CancellationToken);
        Assert.True(fullyQualifiedIntersection.IsSuccess);
        Assert.Single(fullyQualifiedIntersection.Value!.Data);
        Assert.Equal(fantasyKancolleStory.Id, fullyQualifiedIntersection.Value!.Data.First().StoryId);

        // Multi-tag with no overlap
        var noOverlap = await listStories.HandleAsync(
            new ListPublishedStories.ListPublishedStoriesQuery(TagKey: "scifi+kancolle"),
            TestContext.Current.CancellationToken);
        Assert.True(noOverlap.IsSuccess);
        Assert.Empty(noOverlap.Value!.Data);
    }

    [Fact]
    public async Task RenameThenBulkMerge_PreservesSpellingsAndMovesWorkRelationships()
    {
        var source = Tag.CreateCanonical("genre", null, "Sci Fi");
        var secondSource = Tag.CreateCanonical("genre", null, "Space Opera");
        var target = Tag.CreateCanonical("genre", null, "Science Fiction");
        var author = new Data.Authors.Domain.Author { Name = "Taxonomy Admin", UserId = Guid.NewGuid() };
        var story = new Story { Title = "Space Story", Description = "Test", Owner = author, OwnerId = author.Id, PublishedAt = DateTime.UtcNow };
        var secondStory = new Story { Title = "Opera Story", Description = "Test", Owner = author, OwnerId = author.Id, PublishedAt = DateTime.UtcNow };
        story.Tags.Add(source);
        secondStory.Tags.Add(secondSource);
        _context.AddRange(author, source, secondSource, target, story, secondStory);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var principal = Principal(Guid.NewGuid());
        var rename = new RenameCanonicalTag(_context, NullLogger<RenameCanonicalTag>.Instance);
        var renamed = await rename.HandleAsync(
            source.Id,
            new RenameCanonicalTag.RenameCanonicalTagBody("genre", null, "Speculative Fiction", "Clarify display name"),
            principal,
            TestContext.Current.CancellationToken);

        Assert.True(renamed.IsSuccess);
        Assert.Contains("genre:Sci Fi", renamed.Value.Synonyms);

        var merge = new MergeCanonicalTags(_context, NullLogger<MergeCanonicalTags>.Instance);
        var merged = await merge.HandleAsync(
            new MergeCanonicalTags.MergeCanonicalTagsBody(
                target.Id,
                [source.Id.ToString(), secondSource.Id.ToString()],
                "Consolidate overlapping families"),
            principal,
            TestContext.Current.CancellationToken);

        Assert.True(merged.IsSuccess);
        Assert.Equal(2, merged.Value.MergedTagCount);

        _context.ChangeTracker.Clear();
        var persistedTarget = await _context.Tags.OfType<CanonicalTag>()
            .Include(tag => tag.Synonyms)
            .Include(tag => tag.Works)
            .SingleAsync(tag => tag.Id == target.Id, TestContext.Current.CancellationToken);
        Assert.Contains(persistedTarget.Synonyms, synonym => synonym.Value == "Speculative Fiction");
        Assert.Contains(persistedTarget.Synonyms, synonym => synonym.Value == "Sci Fi");
        Assert.Contains(persistedTarget.Synonyms, synonym => synonym.Value == "Space Opera");
        Assert.Contains(persistedTarget.Works, work => work.Id == story.Id);
        Assert.Contains(persistedTarget.Works, work => work.Id == secondStory.Id);
        Assert.False(await _context.Tags.OfType<CanonicalTag>().AnyAsync(
            tag => tag.Id == source.Id || tag.Id == secondSource.Id,
            TestContext.Current.CancellationToken));

        var discovery = new ListPublishedStories(
            _context,
            new PaginationService(Options.Create(new PaginationOptions())));
        var result = await discovery.HandleAsync(
            new ListPublishedStories.ListPublishedStoriesQuery(TagKey: "genre:scifi"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { story.Id, secondStory.Id }.Order().ToArray(),
            result.Value!.Data.Select(item => item.StoryId).ToArray().Order().ToArray());
    }

    [Fact]
    public async Task BulkMerge_RejectsInvalidSelectionsBeforeChangingTaxonomy()
    {
        var source = Tag.CreateCanonical("genre", null, "Cyberpunk");
        var target = Tag.CreateCanonical("genre", null, "Science Fiction");
        _context.AddRange(source, target);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var merge = new MergeCanonicalTags(_context, NullLogger<MergeCanonicalTags>.Instance);
        var principal = Principal(Guid.NewGuid());
        var duplicateSources = await merge.HandleAsync(
            new MergeCanonicalTags.MergeCanonicalTagsBody(
                target.Id,
                [source.Id.ToString(), source.Id.ToString()],
                "Duplicate selection"),
            principal,
            TestContext.Current.CancellationToken);
        var canonicalIncludedAsSource = await merge.HandleAsync(
            new MergeCanonicalTags.MergeCanonicalTagsBody(
                target.Id,
                [target.Id.ToString()],
                "Self merge"),
            principal,
            TestContext.Current.CancellationToken);
        var malformedSource = await merge.HandleAsync(
            new MergeCanonicalTags.MergeCanonicalTagsBody(
                target.Id,
                ["not-a-ulid"],
                "Malformed selection"),
            principal,
            TestContext.Current.CancellationToken);

        Assert.Equal(MergeCanonicalTags.Errors.InvalidSelection, duplicateSources.DomainError);
        Assert.Equal(MergeCanonicalTags.Errors.InvalidSelection, canonicalIncludedAsSource.DomainError);
        Assert.Equal(MergeCanonicalTags.Errors.InvalidSelection, malformedSource.DomainError);
        Assert.Equal(2, await _context.Tags.OfType<CanonicalTag>().CountAsync(TestContext.Current.CancellationToken));
    }

    private static ClaimsPrincipal Principal(Guid userId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
        "Test"));

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "This method implements IConfigureServices<T>.")]
    public static void ConfigureServices(IServiceCollection services) => services
        .AddKeyedTestFictionDbContext<TagTaxonomyIntegrationTests>(configurePendingModelWarning: false);

    public async ValueTask InitializeAsync()
    {
        await _context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        if (_disposed) return;
        await _context.Database.CloseConnectionAsync();
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
        _disposed = true;
        await base.DisposeAsyncCore().ConfigureAwait(false);
    }
}
