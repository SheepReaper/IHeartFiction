using System.Linq.Expressions;

using FluentAssertions;

using IHFiction.FictionApi.Features.Authoring;
using IHFiction.FictionApi.Features.Discovery;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Pagination;
using IHFiction.SharedKernel.Sorting;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IHFiction.UnitTests.Infrastructure;

public sealed class PaginationServiceTests
{
    [Fact]
    public void SourceGenerator_RegistersSortMappingsAndShapersForAnnotatedDtos()
    {
        DataShapingService.HasRegisteredShaper<ListPublishedStories.ListPublishedStoriesItem>().Should().BeTrue();
        DataShapingService.HasRegisteredShaper<ListAuthors.ListAuthorsItem>().Should().BeTrue();
        DataShapingService.HasRegisteredShaper<GetOwnStories.AuthorStoryItem>().Should().BeTrue();
        DataShapingService.HasRegisteredShaper<ListTags.ListTagsItem>().Should().BeTrue();

        var authorMappings = SortMappingRegistry.GetMappings<ListAuthors.ListAuthorsItem>();
        authorMappings.Should().BeEquivalentTo([
            new SortMapping("Name", "Name", false),
            new SortMapping("CreatedAt", "Id", false),
            new SortMapping("UpdatedAt", "UpdatedAt", true)
        ]);

        var storyMappings = SortMappingRegistry.GetMappings<ListPublishedStories.ListPublishedStoriesItem>();
        storyMappings.Select(m => m.SortField).Should().BeEquivalentTo("Title", "PublishedAt", "UpdatedAt");
    }
    [Fact]
    public async Task ExecutePagedQueryAsync_MaterializesPageBeforeContextIsDisposed()
    {
        var options = new DbContextOptionsBuilder<PageContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var service = new PaginationService(Options.Create(new PaginationOptions()));
        ICollectionPaged<PageItem> result;
        await using (var context = new PageContext(options))
        {
            context.Items.AddRange(new PageItem { Id = 1 }, new PageItem { Id = 2 }, new PageItem { Id = 3 });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            result = await service.ExecutePagedQueryAsync(context.Items.OrderBy(x => x.Id),
                new PaginationParams(Page: 2, PageSize: 1), TestContext.Current.CancellationToken);
        }

        result.TotalCount.Should().Be(3);
        result.Data.Select(x => x.Id).Should().Equal(2);
        result.Data.Select(x => x.Id).Should().Equal(2);
    }

    [Fact]
    public async Task ExecutePagedQueryAsync_WithDataShaping_PrunesUnselectedProjectionArgumentsAndPreservesSortAndLinkId()
    {
        var options = new DbContextOptionsBuilder<PageContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var service = new PaginationService(Options.Create(new PaginationOptions()));
        var expensiveProjectionsRun = new int[1];

        PagedCollection<PageItemDto> paged;
        await using (var context = new PageContext(options))
        {
            context.Items.AddRange(
                new PageItem { Id = 1, StoryId = Ulid.NewUlid(), Title = "First", Summary = "Long summary 1", SortRank = 10 },
                new PageItem { Id = 2, StoryId = Ulid.NewUlid(), Title = "Second", Summary = "Long summary 2", SortRank = 30 },
                new PageItem { Id = 3, StoryId = Ulid.NewUlid(), Title = "Third", Summary = "Long summary 3", SortRank = 20 });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            // Sort by SortRank descending (which is NOT in Fields) and project to PageItemDto
            var query = context.Items
                .OrderByDescending(x => x.SortRank)
                .Select(x => new PageItemDto(
                    x.StoryId,
                    x.Title,
                    ComputeExpensiveSummary(x.Summary, expensiveProjectionsRun),
                    x.SortRank));

            paged = await service.ExecutePagedQueryAsync(
                query,
                new PaginationParams(Page: 1, PageSize: 2, Fields: "title"),
                TestContext.Current.CancellationToken);
        }

        // Unselected Summary and SortRank projection arguments were pruned to constants before query execution
        expensiveProjectionsRun[0].Should().Be(0);
        paged.TotalCount.Should().Be(3);

        var materialized = paged.Data.ToList();
        materialized.Select(x => x.Title).Should().Equal("Second", "Third");
        materialized.Should().OnlyContain(x => x.StoryId != Ulid.Empty && x.Summary == string.Empty && x.SortRank == 0);

        // Verify downstream WithLinks + ShapeData preserves links using StoryId while omitting unrequested fields
        var linked = new LinkedPagedCollection<PageItemDto>(
            paged.Data.Select(item => new Linked<PageItemDto>(item, new[] { new LinkItem($"/stories/{item.StoryId}", "self", "GET") })),
            paged.TotalCount,
            paged.CurrentPage,
            paged.PageSize,
            [new LinkItem("/stories/published", "self", "GET")]);

        var shaped = (IDictionary<string, object?>)DataShapingService.ShapeData(linked, "title");
        var shapedItems = ((IEnumerable<object>)shaped["Data"]!).Cast<IDictionary<string, object?>>().ToList();

        shapedItems.Should().HaveCount(2);
        shapedItems[0].Keys.Should().BeEquivalentTo("Title", "links");
        shapedItems[0]["Title"].Should().Be("Second");
        ((IEnumerable<LinkItem>)shapedItems[0]["links"]!).Single().Href.Should().Be($"/stories/{materialized[0].StoryId}");
    }

    [Fact]
    public void ShapeProjection_RewritesSelectNewExpression_KeepingOnlyRequestedFieldsAndPrimaryId()
    {
        var source = new[]
        {
            new PageItem { Id = 1, StoryId = Ulid.NewUlid(), Title = "Alpha", Summary = "Body", SortRank = 5 }
        }.AsQueryable();

        var projected = source.Select(x => new PageItemDto(x.StoryId, x.Title, x.Summary, x.SortRank));
        var shapedQuery = projected.ShapeProjection("title");

        var selectCall = (MethodCallExpression)shapedQuery.Expression;
        var lambda = (LambdaExpression)((UnaryExpression)selectCall.Arguments[1]).Operand;
        var newExpr = (NewExpression)lambda.Body;

        // StoryId (arg 0) and Title (arg 1) remain MemberExpressions; Summary (arg 2) and SortRank (arg 3) are ConstantExpressions
        newExpr.Arguments[0].NodeType.Should().Be(ExpressionType.MemberAccess);
        newExpr.Arguments[1].NodeType.Should().Be(ExpressionType.MemberAccess);
        newExpr.Arguments[2].NodeType.Should().Be(ExpressionType.Constant);
        newExpr.Arguments[3].NodeType.Should().Be(ExpressionType.Constant);
    }

    private static string ComputeExpensiveSummary(string summary, int[] counter)
    {
        counter[0]++;
        return summary;
    }

    private sealed record PageItemDto(Ulid StoryId, string Title, string Summary, int SortRank);

    private sealed class PageContext(DbContextOptions<PageContext> options) : DbContext(options)
    {
        public DbSet<PageItem> Items => Set<PageItem>();
    }

    private sealed class PageItem
    {
        public int Id { get; set; }
        public Ulid StoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public int SortRank { get; set; }
    }
}
