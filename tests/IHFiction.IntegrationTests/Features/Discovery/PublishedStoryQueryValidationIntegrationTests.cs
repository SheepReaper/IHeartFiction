using System.Net;
using System.Text.Json;

using IHFiction.Data.Contexts;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IHFiction.IntegrationTests.Features.Discovery;

public sealed class PublishedStoryQueryValidationIntegrationTests(IntegrationTestWebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/stories")]
    [InlineData("/authors")]
    public async Task OmittedSearch_ReturnsCatalog(string path)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<FictionDbContext>().Database.MigrateAsync(TestContext.Current.CancellationToken);
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("data").ValueKind);
    }

    [Theory]
    [InlineData("/stories?q=x")]
    [InlineData("/authors?q=x")]
    public async Task ShortSearch_PreservesFrameworkValidation(string path)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        Assert.Contains("Search term must be between 2 and 100 characters.",
            problem.RootElement.GetProperty("errors").GetProperty("search").EnumerateArray().Select(error => error.GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("requestId").GetString()));
    }

    [Theory]
    [InlineData("notAField")]
    [InlineData("title sideways")]
    [InlineData(",")]
    public async Task InvalidSort_ReturnsActionableBadRequest(string sort)
    {
        var response = await _client.GetAsync(
            $"/stories?Sort={Uri.EscapeDataString(sort)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        Assert.True(problem.RootElement.TryGetProperty("domainError", out var domainError), problem.RootElement.ToString());
        Assert.Equal("ListPublishedStories.InvalidSort", domainError.GetProperty("code").GetString());
        Assert.Contains("publishedAt, title, updatedAt", domainError.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("notAField")]
    [InlineData("storyId,bogus")]
    [InlineData(",")]
    public async Task InvalidFields_ReturnsActionableBadRequest(string fields)
    {
        var response = await _client.GetAsync(
            $"/stories?Fields={Uri.EscapeDataString(fields)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        Assert.True(problem.RootElement.TryGetProperty("domainError", out var domainError), problem.RootElement.ToString());
        Assert.Equal("ListPublishedStories.InvalidFields", domainError.GetProperty("code").GetString());
        Assert.Contains("storyId, title, description", domainError.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
    }
}


