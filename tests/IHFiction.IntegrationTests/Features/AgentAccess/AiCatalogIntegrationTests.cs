using System.Net;
using System.Text.Json;

using FluentAssertions;

using IHFiction.SharedKernel.AiCatalog;

namespace IHFiction.IntegrationTests.Features.AgentAccess;

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

    [Fact]
    public async Task GetDidWebDocument_Returns200WithW3CDidDocument()
    {
        var response = await _client.GetAsync(
            "/.well-known/did.json",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("id").GetString().Should().Be(AiCatalog.HostIdentifier);
        root.GetProperty("@context")[0].GetString().Should().Be("https://www.w3.org/ns/did/v1");
        root.GetProperty("service").GetArrayLength().Should().Be(4);
    }

    [Fact]
    public async Task GetApiCatalog_ReturnsStandardOpenApiMediaType()
    {
        var response = await _client.GetAsync(
            "/.well-known/api-catalog",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(json);
        var serviceDesc = doc.RootElement
            .GetProperty("linkset")[0]
            .GetProperty("service-desc")[0];

        serviceDesc.GetProperty("type").GetString().Should().Be("application/vnd.oai.openapi+json");
    }
}
