using System.Text.Json;

using IHFiction.SharedKernel.ApiCatalog;

namespace IHFiction.UnitTests.SharedKernel.ApiCatalog;

public sealed class LinksetDocumentTests
{
    [Fact]
    public void Serialize_OmitsUnusedOptionalLinkRelations()
    {
        LinksetDocument document = new([
            new(
                Anchor: "https://api.example.test/",
                ServiceDescription: [new("https://api.example.test/openapi.json")],
                ServiceDocumentation: [new("https://api.example.test/docs", "text/html")])
        ]);

        var json = JsonSerializer.Serialize(document);

        Assert.DoesNotContain(":null", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"item\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"title\"", json, StringComparison.Ordinal);
        Assert.Contains("\"service-desc\"", json, StringComparison.Ordinal);
        Assert.Contains("\"service-doc\"", json, StringComparison.Ordinal);
    }
}
