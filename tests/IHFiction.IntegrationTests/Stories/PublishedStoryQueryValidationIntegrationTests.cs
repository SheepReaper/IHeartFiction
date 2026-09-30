using System.Net;
using System.Text.Json;

namespace IHFiction.IntegrationTests.Stories;

public sealed class PublishedStoryQueryValidationIntegrationTests(IntegrationTestWebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("notAField")]
    [InlineData("title sideways")]
    [InlineData(",")]
    public async Task InvalidSort_ReturnsActionableBadRequest(string sort)
    {
        var response = await _client.GetAsync(
            $"/stories/published?Sort={Uri.EscapeDataString(sort)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var domainError = problem.RootElement.GetProperty("domainError");
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
            $"/stories/published?Fields={Uri.EscapeDataString(fields)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var domainError = problem.RootElement.GetProperty("domainError");
        Assert.Equal("ListPublishedStories.InvalidFields", domainError.GetProperty("code").GetString());
        Assert.Contains("storyId, title, description", domainError.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);
    }
}


