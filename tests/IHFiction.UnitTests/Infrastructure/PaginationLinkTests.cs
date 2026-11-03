namespace IHFiction.UnitTests.Infrastructure;

public sealed class PaginationLinkTests
{
    [Theory]
    [InlineData(0, "q")]
    [InlineData(1, "q")]
    [InlineData(2, "q")]
    [InlineData(3, "q")]
    [InlineData(4, "q")]
    [InlineData(5, null)]
    public async Task CollectionLinks_PreserveBoundQueryNamesAndPagination(int kind, string? searchKey)
    {
        const string search = "Magic & dragons + café";
        object? query = kind switch
        {
            0 => new ListPublishedStories.ListPublishedStoriesQuery(Search: search, Fields: "storyId,title", Sort: "title desc"),
            1 => new ListAuthors.ListAuthorsQuery(Search: search, Fields: "id,name", Sort: "name desc"),
            2 => new ListTags.ListTagsQuery(Search: search, Fields: "id,value", Sort: "value desc"),
            3 => new GetOwnStories.GetOwnStoriesQuery(Search: search, Fields: "id,title", Sort: "title desc"),
            4 => new UnaliasedSearch(search),
            _ => null
        };
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["AllowedHosts"] = "*";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.MapGet("/stories/{id}/chapters", () => "unused").WithName("collection");
        await app.StartAsync(TestContext.Current.CancellationToken);
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.test");
        var linker = new LinkService(app.Services.GetRequiredService<LinkGenerator>(), new HttpContextAccessor { HttpContext = context });

        for (var page = 1; page <= 3; page++)
        {
            var collection = new PagedCollection<int>(Array.Empty<int>().AsQueryable(), 3, page, 1);
            var linked = collection.WithLinks(linker, "collection", queryParams: query,
                routeValues: [new("id", "story-id")]);
            Assert.Equal(page < 3, linked.Links.Any(link => link.Rel == "next-page"));
            Assert.Equal(page > 1, linked.Links.Any(link => link.Rel == "previous-page"));
            foreach (var link in linked.Links)
            {
                var uri = new Uri(link.Href);
                Assert.Equal("/stories/story-id/chapters", uri.AbsolutePath);
                var values = QueryHelpers.ParseQuery(uri.Query);
                Assert.Equal("1", values["PageSize"]);
                var expectedPage = link.Rel switch { "next-page" => page + 1, "previous-page" => page - 1, _ => page };
                Assert.Equal(expectedPage.ToString(System.Globalization.CultureInfo.InvariantCulture), values["Page"]);
                if (searchKey is not null)
                {
                    Assert.Equal(search, values[searchKey]);
                    Assert.Contains(values.Keys, key => string.Equals(key, "q", StringComparison.Ordinal));
                    Assert.False(values.ContainsKey("Search"));
                }
                else
                {
                    Assert.False(values.ContainsKey("Search"));
                    Assert.False(values.ContainsKey("q"));
                }
                if (query is IHFiction.SharedKernel.DataShaping.IDataShapingSupport shaping)
                {
                    Assert.Equal(shaping.Fields, values["Fields"]);
                    Assert.Equal(((IHFiction.SharedKernel.Sorting.ISortingSupport)query).Sort, values["Sort"]);
                }
            }
        }
    }

    private sealed record UnaliasedSearch(string Search) : ISearchSupport;
}
