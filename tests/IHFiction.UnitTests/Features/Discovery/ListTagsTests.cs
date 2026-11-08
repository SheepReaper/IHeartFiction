using System.Text.Json;

using Cysharp.Serialization.Json;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.Data.Searching.Domain;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Features.Discovery;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Pagination;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Features.Discovery;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IHFiction.UnitTests.Features.Discovery;

/// <summary>
/// Unit tests for ListTags functionality
/// Tests request validation, response model construction, and query parameter handling
/// </summary>
public class ListTagsTests
{
    // Note: Request validation tests removed as they test low-value implementation details.
    // Validation is handled automatically by .NET 10's AddValidation() system with
    // business logic validation in the endpoint implementation.

    [Fact]
    public void PaginatedCollectionResponse_CanBeCreated()
    {
        // Arrange
        var tags = new List<ListTags.ListTagsItem>
        {
            new(Ulid.NewUlid(), "genre", null, "fantasy", DateTime.UtcNow, 15, "genre:fantasy", "genre:fantasy"),
            new(Ulid.NewUlid(), "theme", null, "adventure", DateTime.UtcNow, 8, "theme:adventure", "theme:adventure")
        };

        // Act
        var response = new PagedCollection<ListTags.ListTagsItem>(
            tags.AsQueryable(),
            125, // TotalCount
            2,   // CurrentPage
            50); // PageSize

        // Assert
        Assert.Equal(2, response.Data.Count());
        Assert.Equal(125, response.TotalCount);
        Assert.Equal(3, response.TotalPages);
        Assert.Equal(2, response.CurrentPage);
        Assert.Equal(50, response.PageSize);
        Assert.True(response.HasNextPage);
        Assert.True(response.HasPreviousPage);
    }

    [Fact]
    public async Task HandleAsync_FiltersByCategoryAndSubcategory()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var genreGeneral = Tag.CreateCanonical("genre", null, "Fantasy");
        var genreSubgenre = Tag.CreateCanonical("genre", "subgenre", "Space Opera");
        var characterHero = Tag.CreateCanonical("character", "protagonist", "Taylor Hebert");
        context.AddRange(genreGeneral, genreSubgenre, characterHero);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new ListTags(context, new PaginationService(Options.Create(new PaginationOptions())));

        var categoryResult = await useCase.HandleAsync(
            new ListTags.ListTagsQuery(Category: "GENRE", Sort: "value"),
            TestContext.Current.CancellationToken);
        Assert.True(categoryResult.IsSuccess);
        Assert.Equal(2, categoryResult.Value.TotalCount);
        Assert.All(categoryResult.Value.Data, item => Assert.Equal("genre", item.Category));

        var subcategoryResult = await useCase.HandleAsync(
            new ListTags.ListTagsQuery(Subcategory: "Subgenre"),
            TestContext.Current.CancellationToken);
        Assert.True(subcategoryResult.IsSuccess);
        var singleSubcategory = Assert.Single(subcategoryResult.Value.Data);
        Assert.Equal(genreSubgenre.Id, singleSubcategory.TagId);
        Assert.Equal("subgenre", singleSubcategory.Subcategory);

        var combinedResult = await useCase.HandleAsync(
            new ListTags.ListTagsQuery(Category: "genre", Subcategory: "protagonist"),
            TestContext.Current.CancellationToken);
        Assert.True(combinedResult.IsSuccess);
        Assert.Empty(combinedResult.Value.Data);
    }

    [Fact]
    public async Task MapEndpoint_BodyFreeGet_BindsCategoryAndSubcategoryAndPreservesFiltersAcrossPages()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using (var seedContext = CreateContext(databaseName))
        {
            var author = new Author { Id = Ulid.NewUlid(), UserId = Guid.NewGuid(), Name = "Author" };
            var genreAdventure = Tag.CreateCanonical("genre", null, "Adventure");
            var genreSpaceOpera = Tag.CreateCanonical("genre", "subgenre", "Space Opera");
            var characterTag = Tag.CreateCanonical("character", "protagonist", "Hero");
            var story = new Story
            {
                Title = "Published Story",
                Description = "Published description",
                Owner = author,
                OwnerId = author.Id,
                PublishedAt = DateTime.UtcNow
            };
            story.Tags.Add(genreAdventure);
            story.Tags.Add(genreSpaceOpera);
            story.Tags.Add(characterTag);
            seedContext.AddRange(author, genreAdventure, genreSpaceOpera, characterTag, story);
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var builder = WebApplication.CreateBuilder();
        builder.Configuration["AllowedHosts"] = "*";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddPagination();
        builder.Services.AddScoped<LinkService>();
        builder.Services.AddScoped(_ => CreateContext(databaseName));
        builder.Services.AddScoped<ListTags>();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(new UlidJsonConverter());
            options.SerializerOptions.Converters.Add(new LinkedConverterFactory());
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, FictionApiJsonSerializerContext.Default);
        });

        await using var app = builder.Build();
        var endpoint = new ListTags.Endpoint();
        endpoint.MapEndpoint(app).WithName(endpoint.Name);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var baseAddress = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses.Single();
        using var httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };

        // 1. Body-free GET /tags?Category=genre&PageSize=1&Sort=value
        using var firstPageResponse = await httpClient.GetAsync(
            new Uri("/tags?Category=genre&PageSize=1&Sort=value", UriKind.Relative),
            TestContext.Current.CancellationToken);
        Assert.True(firstPageResponse.IsSuccessStatusCode);
        using var firstPageDoc = JsonDocument.Parse(
            await firstPageResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = firstPageDoc.RootElement;
        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("currentPage").GetInt32());
        var firstItem = Assert.Single(root.GetProperty("data").EnumerateArray());
        Assert.Equal("genre", firstItem.GetProperty("category").GetString());
        Assert.Equal("Adventure", firstItem.GetProperty("value").GetString());

        var nextHref = root.GetProperty("links").EnumerateArray()
            .Single(link => link.GetProperty("rel").GetString() == "next-page")
            .GetProperty("href").GetString()!;
        var nextQuery = QueryHelpers.ParseQuery(new Uri(nextHref).Query);
        Assert.Equal("genre", nextQuery["Category"]);
        Assert.Equal("2", nextQuery["Page"]);
        Assert.Equal("1", nextQuery["PageSize"]);

        // 2. Follow next-page link directly and verify filtered page 2
        using var secondPageResponse = await httpClient.GetAsync(
            new Uri(nextHref),
            TestContext.Current.CancellationToken);
        Assert.True(secondPageResponse.IsSuccessStatusCode);
        using var secondPageDoc = JsonDocument.Parse(
            await secondPageResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var secondRoot = secondPageDoc.RootElement;
        Assert.Equal(2, secondRoot.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, secondRoot.GetProperty("currentPage").GetInt32());
        var secondItem = Assert.Single(secondRoot.GetProperty("data").EnumerateArray());
        Assert.Equal("genre", secondItem.GetProperty("category").GetString());
        Assert.Equal("subgenre", secondItem.GetProperty("subcategory").GetString());
        Assert.Equal("Space Opera", secondItem.GetProperty("value").GetString());

        var prevHref = secondRoot.GetProperty("links").EnumerateArray()
            .Single(link => link.GetProperty("rel").GetString() == "previous-page")
            .GetProperty("href").GetString()!;
        var prevQuery = QueryHelpers.ParseQuery(new Uri(prevHref).Query);
        Assert.Equal("genre", prevQuery["Category"]);
        Assert.Equal("1", prevQuery["Page"]);

        // 3. Lowercase query parameter names & Subcategory filter + generated TagService caller
        var apiClient = new FictionApiClient(httpClient);
        var tagService = new TagService(apiClient);
        var serviceResult = await tagService.ListAsync(
            page: 1,
            pageSize: 10,
            category: "genre",
            subcategory: "subgenre",
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(serviceResult.IsSuccess);
        var serviceItem = Assert.Single(serviceResult.Value.Data);
        Assert.Equal("genre", serviceItem.Category);
        Assert.Equal("subgenre", serviceItem.Subcategory);
        Assert.Equal("Space Opera", serviceItem.Value);
        var selfLink = Assert.Single(serviceResult.Value.Links, link => link.Rel == "self");
        var selfQuery = QueryHelpers.ParseQuery(new Uri(selfLink.Href).Query);
        Assert.Equal("genre", selfQuery["Category"]);
        Assert.Equal("subgenre", selfQuery["Subcategory"]);
    }

    private static FictionDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<FictionDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new FictionDbContext(options);
    }
}
