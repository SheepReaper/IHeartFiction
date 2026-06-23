using System.Net;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;

using IHFiction.Data.Contexts;
using IHFiction.SharedWeb.Reporting;

namespace IHFiction.UnitTests.Reporting;

public class CspReportStorageTests
{
    [Fact]
    public void DeserializeReports_ModernCspViolation_ReturnsCspViolationReport()
    {
        const string body = """
            [{
              "age": 10,
              "type": "csp-violation",
              "url": "https://iheartfiction.net/read/story",
              "user_agent": "test-agent",
              "body": {
                "blockedURL": "https://cdn.example.com/script.js",
                "documentURL": "https://iheartfiction.net/read/story?utm=1",
                "effectiveDirective": "script-src-elem",
                "statusCode": 200
              }
            }]
            """;

        var reports = BrowserReportParser.DeserializeReports("application/reports+json", body);

        var report = reports.Should().ContainSingle().Subject.Should().BeOfType<CspViolationReport>().Subject;
        report.Type.Should().Be("csp-violation");
        report.UserAgent.Should().Be("test-agent");
        report.Body.BlockedUrl.Should().Be("https://cdn.example.com/script.js");
        report.Body.DocumentUrl.Should().Be("https://iheartfiction.net/read/story?utm=1");
        report.Body.EffectiveDirective.Should().Be("script-src-elem");
        report.Body.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void DeserializeReports_LegacyCspReport_TransformsToModernCspViolation()
    {
        const string body = """
            {
              "csp-report": {
                "blocked-uri": "inline",
                "document-uri": "https://iheartfiction.net/",
                "effective-directive": "script-src",
                "script-sample": "alert(1)",
                "status-code": 200
              }
            }
            """;

        var reports = BrowserReportParser.DeserializeReports("application/csp-report", body);

        var report = reports.Should().ContainSingle().Subject.Should().BeOfType<CspViolationReport>().Subject;
        report.Type.Should().Be("csp-violation");
        report.Body.BlockedUrl.Should().Be("inline");
        report.Body.DocumentUrl.Should().Be("https://iheartfiction.net/");
        report.Body.EffectiveDirective.Should().Be("script-src");
        report.Body.Sample.Should().Be("alert(1)");
    }

    [Fact]
    public void DeserializeReports_UnknownModernType_PreservesGenericPayload()
    {
        const string body = """
            [{
              "age": 1,
              "type": "mystery",
              "url": "https://iheartfiction.net/",
              "user_agent": "test-agent",
              "body": { "id": "example", "message": "deprecated" }
            }]
            """;

        var reports = BrowserReportParser.DeserializeReports("application/reports+json", body);

        var report = reports.Should().ContainSingle().Subject.Should().BeOfType<GenericBrowserReport>().Subject;
        report.Type.Should().Be("mystery");
        report.Payload.GetProperty("body").GetProperty("id").GetString().Should().Be("example");
    }

    [Theory]
    [MemberData(nameof(KnownReportSamples))]
    public void DeserializeReports_KnownModernTypes_ReturnsTypedReport(string json, Type expectedType)
    {
        var reports = BrowserReportParser.DeserializeReports("application/reports+json", json);

        reports.Should().ContainSingle().Subject.Should().BeOfType(expectedType);
    }

    [Fact]
    public void CreateCspFingerprint_IgnoresClientSpecificFields()
    {
        var first = CreateCspReport(userAgent: "agent-1", sample: "sample-1", lineNumber: 1, columnNumber: 2);
        var second = CreateCspReport(userAgent: "agent-2", sample: "sample-2", lineNumber: 10, columnNumber: 20);

        CspReportStorageService.CreateCspFingerprint(first)
            .Should()
            .Be(CspReportStorageService.CreateCspFingerprint(second));
    }

    [Fact]
    public void CreateCspFingerprint_ChangesForPolicyReviewFields()
    {
        var baseline = CspReportStorageService.CreateCspFingerprint(CreateCspReport());

        CspReportStorageService.CreateCspFingerprint(CreateCspReport(effectiveDirective: "img-src")).Should().NotBe(baseline);
        CspReportStorageService.CreateCspFingerprint(CreateCspReport(blockedUrl: "https://cdn.example.com/other.js")).Should().NotBe(baseline);
        CspReportStorageService.CreateCspFingerprint(CreateCspReport(documentUrl: "https://iheartfiction.net/stories")).Should().NotBe(baseline);
        CspReportStorageService.CreateCspFingerprint(CreateCspReport(sourceFile: "https://iheartfiction.net/other.js")).Should().NotBe(baseline);
        CspReportStorageService.CreateCspFingerprint(CreateCspReport(statusCode: HttpStatusCode.NotFound)).Should().NotBe(baseline);
    }

    [Fact]
    public void CreatePayloadHash_SortsObjectProperties()
    {
        var first = CreateGenericReport("""{ "type": "mystery", "body": { "b": 2, "a": 1 }, "url": "https://localhost:7000" }""");
        var second = CreateGenericReport("""{ "body": { "a": 1, "b": 2 }, "type": "mystery", "url": "https://localhost:7000" }""");

        CspReportStorageService.CreatePayloadHash(first)
            .Should()
            .Be(CspReportStorageService.CreatePayloadHash(second));
    }

    [Fact]
    public void CreatePayloadHash_IgnoresWrapperMetadata()
    {
        var first = BrowserReportParser.DeserializeReports("application/reports+json", """
            [{
              "age": 1,
              "type": "deprecation",
              "url": "https://iheartfiction.net/first",
              "user_agent": "first-agent",
              "body": { "id": "old-api", "anticipatedRemoval": "2027-01-01T00:00:00+00:00", "message": "Deprecated" }
            }]
            """).Single();
        var second = BrowserReportParser.DeserializeReports("application/reports+json", """
            [{
              "age": 99,
              "type": "deprecation",
              "url": "https://iheartfiction.net/second",
              "user_agent": "second-agent",
              "body": { "message": "Deprecated", "anticipatedRemoval": "2027-01-01T00:00:00+00:00", "id": "old-api" }
            }]
            """).Single();

        CspReportStorageService.CreatePayloadHash(first)
            .Should()
            .Be(CspReportStorageService.CreatePayloadHash(second));
    }

    [Fact]
    public async Task StoreAsync_FirstCspViolation_InsertsRow()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.StoreAsync([CreateCspReport()], TestContext.Current.CancellationToken);

        var stored = await context.CspViolationReports.SingleAsync(TestContext.Current.CancellationToken);
        stored.OccurrenceCount.Should().Be(1);
        stored.EffectiveDirective.Should().Be("script-src-elem");
        stored.BlockedResource.Should().Be("https://cdn.example.com/script.js");
        stored.DocumentResource.Should().Be("https://iheartfiction.net/read/story?utm=1");
    }

    [Fact]
    public async Task StoreAsync_DuplicateCspViolation_UpdatesExistingRow()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.StoreAsync([CreateCspReport(userAgent: "first", sample: "old")], TestContext.Current.CancellationToken);
        await service.StoreAsync([CreateCspReport(userAgent: "second", sample: "new", lineNumber: 42)], TestContext.Current.CancellationToken);

        var stored = await context.CspViolationReports.SingleAsync(TestContext.Current.CancellationToken);
        stored.OccurrenceCount.Should().Be(2);
        stored.LastUserAgent.Should().Be("second");
        stored.LastSample.Should().Be("new");
        stored.LastLineNumber.Should().Be(42);
    }

    [Fact]
    public async Task StoreAsync_GenericReports_DeduplicatesByTypeAndCanonicalPayload()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.StoreAsync([CreateGenericReport("""{ "type": "mystery", "body": { "b": 2, "a": 1 }, "user_agent": "first", "url": "https://localhost:7000" }""")], TestContext.Current.CancellationToken);
        await service.StoreAsync([CreateGenericReport("""{ "user_agent": "second", "body": { "a": 1, "b": 2 }, "type": "mystery", "url": "https://localhost:7000" }""")], TestContext.Current.CancellationToken);

        var stored = await context.BrowserReportPayloads.SingleAsync(TestContext.Current.CancellationToken);
        stored.ReportType.Should().Be("mystery");
        stored.OccurrenceCount.Should().Be(2);
    }

    [Theory]
    [MemberData(nameof(StorageReportSamples))]
    public async Task StoreAsync_KnownNonCspReports_PopulatesSummaryAndDeduplicates(
        string firstJson,
        string secondJson,
        string reportType,
        string? summaryKey,
        string? summaryMessage,
        string? blockedResource,
        string? sourceFile,
        int? lineNumber,
        int? columnNumber,
        string? disposition)
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.StoreAsync(BrowserReportParser.DeserializeReports("application/reports+json", firstJson), TestContext.Current.CancellationToken);
        await service.StoreAsync(BrowserReportParser.DeserializeReports("application/reports+json", secondJson), TestContext.Current.CancellationToken);

        var stored = await context.BrowserReportPayloads.SingleAsync(TestContext.Current.CancellationToken);
        stored.ReportType.Should().Be(reportType);
        stored.OccurrenceCount.Should().Be(2);
        stored.ReportResource.Should().Be("https://iheartfiction.net/first");
        stored.SummaryKey.Should().Be(summaryKey);
        stored.SummaryMessage.Should().Be(summaryMessage);
        stored.BlockedResource.Should().Be(blockedResource);
        stored.SourceFile.Should().Be(sourceFile);
        stored.LineNumber.Should().Be(lineNumber);
        stored.ColumnNumber.Should().Be(columnNumber);
        stored.Disposition.Should().Be(disposition);
    }

    private static CspViolationReport CreateCspReport(
        string effectiveDirective = "script-src-elem",
        string blockedUrl = "https://cdn.example.com/script.js",
        string documentUrl = "https://iheartfiction.net/read/story?utm=1",
        string sourceFile = "https://iheartfiction.net/app.js?v=1",
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string? userAgent = "test-agent",
        string? sample = "sample",
        int? lineNumber = 1,
        int? columnNumber = 2)
    {
        return new(
            new(
                blockedUrl,
                columnNumber,
                "report",
                documentUrl,
                effectiveDirective,
                lineNumber,
                "default-src 'none'",
                null,
                sample,
                sourceFile,
                statusCode),
            "csp-violation",
            new("https://iheartfiction.net/read/story"),
            0,
            userAgent);
    }

    private static GenericBrowserReport CreateGenericReport(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement.Clone();

        if(!(root.TryGetProperty("url", out var url) && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri)))
        {
            throw new ArgumentNullException("url was not provided");
        }

        if(!(root.TryGetProperty("type", out var type) && type.GetString() is {} typeStr && !string.IsNullOrWhiteSpace(typeStr)))
        {
            throw new ArgumentNullException("type was not provided");
        }

        return new(
            typeStr,
            uri,
            root);
    }

    public static TheoryData<string, Type> KnownReportSamples() => new()
    {
        { CoepFirstJson, typeof(CoepViolationReport) },
        { CrashFirstJson, typeof(CrashReport) },
        { DeprecationFirstJson, typeof(DeprecationReport) },
        { IntegrityFirstJson, typeof(IntegrityViolationReport) },
        { InterventionFirstJson, typeof(InterventionReport) },
        { PermissionsPolicyFirstJson, typeof(PermissionsPolicyViolationReport) }
    };

    public static TheoryData<string, string, string, string?, string?, string?, string?, int?, int?, string?> StorageReportSamples() => new()
    {
        { CoepFirstJson, CoepSecondJson, "coep", "corp", null, "https://cdn.example.com/frame.html", null, null, null, "enforce" },
        { CrashFirstJson, CrashSecondJson, "crash", "oom", "hidden", null, null, null, null, null },
        { DeprecationFirstJson, DeprecationSecondJson, "deprecation", "old-api", "Deprecated", null, "https://iheartfiction.net/app.js", 7, 3, null },
        { IntegrityFirstJson, IntegritySecondJson, "integrity-violation", "script", null, "https://cdn.example.com/script.js", null, null, null, "report" },
        { InterventionFirstJson, InterventionSecondJson, "intervention", "blocked-parser", "Parser blocked", null, "https://iheartfiction.net/app.js", 8, 4, null },
        { PermissionsPolicyFirstJson, PermissionsPolicySecondJson, "permissions-policy-violation", "camera", "Camera denied", null, "https://iheartfiction.net/app.js", 9, 5, "enforce" }
    };

    private const string CoepFirstJson = """
        [{
          "type": "coep",
          "url": "https://iheartfiction.net/first",
          "body": { "type": "corp", "blockedURL": "https://cdn.example.com/frame.html", "destination": "iframe", "disposition": "enforce" }
        }]
        """;

    private const string CoepSecondJson = """
        [{
          "type": "coep",
          "url": "https://iheartfiction.net/second",
          "body": { "disposition": "enforce", "destination": "iframe", "blockedURL": "https://cdn.example.com/frame.html", "type": "corp" }
        }]
        """;

    private const string CrashFirstJson = """
        [{
          "age": 1,
          "type": "crash",
          "url": "https://iheartfiction.net/first",
          "user_agent": "first-agent",
          "body": { "is_top_level": true, "reason": "oom", "visibility_state": "hidden" }
        }]
        """;

    private const string CrashSecondJson = """
        [{
          "age": 9,
          "type": "crash",
          "url": "https://iheartfiction.net/second",
          "user_agent": "second-agent",
          "body": { "visibility_state": "hidden", "reason": "oom", "is_top_level": true }
        }]
        """;

    private const string DeprecationFirstJson = """
        [{
          "type": "deprecation",
          "url": "https://iheartfiction.net/first",
          "body": { "id": "old-api", "anticipatedRemoval": "2027-01-01T00:00:00+00:00", "message": "Deprecated", "sourceFile": "https://iheartfiction.net/app.js", "lineNumber": 7, "columnNumber": 3 }
        }]
        """;

    private const string DeprecationSecondJson = """
        [{
          "type": "deprecation",
          "url": "https://iheartfiction.net/second",
          "body": { "columnNumber": 3, "lineNumber": 7, "sourceFile": "https://iheartfiction.net/app.js", "message": "Deprecated", "anticipatedRemoval": "2027-01-01T00:00:00+00:00", "id": "old-api" }
        }]
        """;

    private const string IntegrityFirstJson = """
        [{
          "type": "integrity-violation",
          "url": "https://iheartfiction.net/first",
          "body": { "blockedURL": "https://cdn.example.com/script.js", "documentURL": "https://iheartfiction.net/", "destination": "script", "reportOnly": true }
        }]
        """;

    private const string IntegritySecondJson = """
        [{
          "type": "integrity-violation",
          "url": "https://iheartfiction.net/second",
          "body": { "reportOnly": true, "destination": "script", "documentURL": "https://iheartfiction.net/", "blockedURL": "https://cdn.example.com/script.js" }
        }]
        """;

    private const string InterventionFirstJson = """
        [{
          "type": "intervention",
          "url": "https://iheartfiction.net/first",
          "body": { "id": "blocked-parser", "message": "Parser blocked", "sourceFile": "https://iheartfiction.net/app.js", "lineNumber": 8, "columnNumber": 4 }
        }]
        """;

    private const string InterventionSecondJson = """
        [{
          "type": "intervention",
          "url": "https://iheartfiction.net/second",
          "body": { "columnNumber": 4, "lineNumber": 8, "sourceFile": "https://iheartfiction.net/app.js", "message": "Parser blocked", "id": "blocked-parser" }
        }]
        """;

    private const string PermissionsPolicyFirstJson = """
        [{
          "type": "permissions-policy-violation",
          "url": "https://iheartfiction.net/first",
          "body": { "featureId": "camera", "message": "Camera denied", "disposition": "enforce", "sourceFile": "https://iheartfiction.net/app.js", "lineNumber": 9, "columnNumber": 5 }
        }]
        """;

    private const string PermissionsPolicySecondJson = """
        [{
          "type": "permissions-policy-violation",
          "url": "https://iheartfiction.net/second",
          "body": { "columnNumber": 5, "lineNumber": 9, "sourceFile": "https://iheartfiction.net/app.js", "disposition": "enforce", "message": "Camera denied", "featureId": "camera" }
        }]
        """;

    private static CspReportStorageService CreateService(FictionDbContext context)
    {
        return new(context, NullLogger<CspReportStorageService>.Instance);
    }

    private static FictionDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FictionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FictionDbContext(options);
    }
}
