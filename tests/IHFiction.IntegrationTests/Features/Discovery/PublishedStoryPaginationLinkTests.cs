using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.Data.Stories.Domain;

namespace IHFiction.IntegrationTests.Features.Discovery;

public sealed class PublishedStoryPaginationLinkTests(IntegrationTestWebAppFactory factory) : IConfigureServices<PublishedStoryPaginationLinkTests>
{
    [Theory]
    [InlineData("/stories")]
    [InlineData("/authors")]
    [InlineData("/tags")]
    [InlineData("/me/stories")]
    public async Task OpenApi_UsesCanonicalLowercaseSearchParameter(string path)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var names = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("get")
            .GetProperty("parameters").EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString()).ToArray();
        Assert.Contains("q", names);
        Assert.DoesNotContain("Q", names);
        Assert.DoesNotContain("Search", names);
    }

    [Fact]
    public async Task GeneratedLinks_ReplaySearchFiltersSortAndFieldSelectionThroughEndpointBinding()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredKeyedService<FictionDbContext>(nameof(PublishedStoryPaginationLinkTests));
        await context.Database.MigrateAsync(cancellationToken);
        var author = new Author { Name = "Pagination Author", UserId = Guid.NewGuid(), Profile = new Profile { Bio = "Test" } };
        var other = new Author { Name = "Other Author", UserId = Guid.NewGuid(), Profile = new Profile { Bio = "Test" } };
        var tag = Tag.CreateCanonical("theme", null, "Pagination");
        var otherTag = Tag.CreateCanonical("theme", null, "Other");
        var matches = Enumerable.Range(1, 3).Select(index => Story($"Magic café {index}", author, tag)).ToArray();
        var wrongStatus = Story("Magic café wrong status", author, tag);
        wrongStatus.CompletionStatus = StoryCompletionStatus.InProgress;
        var unpublished = Story("Magic café unpublished", author, tag);
        unpublished.PublishedAt = null;
        context.AddRange(matches);
        context.AddRange(wrongStatus, unpublished, Story("Unrelated", author, tag),
            Story("Magic café other author", other, tag), Story("Magic café other tag", author, otherTag));
        await context.SaveChangesAsync(cancellationToken);

        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped(provider => provider.GetRequiredKeyedService<FictionDbContext>(nameof(PublishedStoryPaginationLinkTests)))));
        using var client = app.CreateClient();
        var filters = new Dictionary<string, string?>
        {
            ["q"] = "Magic café", ["AuthorId"] = author.Id.ToString(), ["CompletionStatus"] = "Complete",
            ["TagKey"] = tag.NormalizedKey, ["Fields"] = "storyId,title", ["Sort"] = "title asc", ["PageSize"] = "1"
        };
        string? next = QueryHelpers.AddQueryString("/stories", filters);
        var visited = new List<string>();
        for (var page = 1; page <= 3; page++)
        {
            using var response = await ReadAsync(next!);
            Assert.Equal(3, response.RootElement.GetProperty("totalCount").GetInt32());
            Assert.Equal(page, response.RootElement.GetProperty("currentPage").GetInt32());
            var item = Assert.Single(response.RootElement.GetProperty("data").EnumerateArray());
            Assert.Equal(matches[page - 1].Id.ToString(), item.GetProperty("storyId").GetString());
            Assert.Equal(matches[page - 1].Title, item.GetProperty("title").GetString());
            Assert.False(item.TryGetProperty("description", out _));
            Assert.NotEmpty(item.GetProperty("links").EnumerateArray());
            var links = response.RootElement.GetProperty("links").EnumerateArray()
                .ToDictionary(link => link.GetProperty("rel").GetString()!, link => link.GetProperty("href").GetString()!);
            foreach (var href in links.Values)
            {
                var query = QueryHelpers.ParseQuery(new Uri(href).Query);
                foreach (var filter in filters) Assert.Equal(filter.Value, query[filter.Key]);
                Assert.False(query.ContainsKey("Search"));
            }
            using var self = await ReadAsync(links["self"]);
            Assert.Equal(item.GetRawText(), Assert.Single(self.RootElement.GetProperty("data").EnumerateArray()).GetRawText());
            Assert.Equal(page > 1, links.ContainsKey("previous-page"));
            if (page > 1)
            {
                using var previous = await ReadAsync(links["previous-page"]);
                Assert.Equal(visited[^1], Assert.Single(previous.RootElement.GetProperty("data").EnumerateArray()).GetProperty("storyId").GetString());
            }
            visited.Add(item.GetProperty("storyId").GetString()!);
            Assert.Equal(page < 3, links.TryGetValue("next-page", out next));
        }

        async Task<JsonDocument> ReadAsync(string url)
        {
            using var response = await client.GetAsync(url, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        }
    }

    private static Story Story(string title, Author author, Tag tag)
    {
        var story = new Story { Title = title, Description = "Test", Owner = author, OwnerId = author.Id,
            PublishedAt = DateTime.UtcNow, CompletionStatus = StoryCompletionStatus.Complete };
        story.Authors.Add(author);
        story.Tags.Add(tag);
        return story;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "Implements IConfigureServices<T>.")]
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddKeyedTestFictionDbContext<PublishedStoryPaginationLinkTests>(configurePendingModelWarning: false);
        services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                new OpenIdConnectConfiguration
                {
                    AuthorizationEndpoint = "https://auth.example.test/realms/fiction/protocol/openid-connect/auth",
                    TokenEndpoint = "https://auth.example.test/realms/fiction/protocol/openid-connect/token"
                }));
    }
}
