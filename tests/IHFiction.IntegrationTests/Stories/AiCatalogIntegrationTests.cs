using System.Net;
using System.Text.Json;

using FluentAssertions;

using IHFiction.SharedKernel.AiCatalog;

namespace IHFiction.IntegrationTests.Stories;

public sealed class AiCatalogIntegrationTests(IntegrationTestWebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetAiCatalog_Returns200WithSpecVersion10AndEntries()
    {
        var response = await _client.GetAsync(
            "/.well-known/ai-catalog.json",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("specVersion").GetString().Should().Be(AiCatalog.SpecVersion);

        var host = root.GetProperty("host");
        host.GetProperty("displayName").GetString().Should().Be(AiCatalog.HostDisplayName);
        host.GetProperty("identifier").GetString().Should().Be(AiCatalog.HostIdentifier);

        var entries = root.GetProperty("entries");
        entries.GetArrayLength().Should().Be(4);
    }

    [Fact]
    public async Task HeadAiCatalog_Returns200()
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, "/.well-known/ai-catalog.json");
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
