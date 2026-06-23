using System.Text.Json;
using System.Text.Json.Serialization;

namespace IHFiction.SharedWeb.Reporting;

internal sealed class BrowserReportJsonConverter : JsonConverter<BrowserReport>
{
    private const string CspViolationReportType = "csp-violation";
    private const string CoepReportType = "coep";
    private const string CrashReportType = "crash";
    private const string DeprecationReportType = "deprecation";
    private const string IntegrityViolationReportType = "integrity-violation";
    private const string InterventionReportType = "intervention";
    private const string PermissionsPolicyViolationReportType = "permissions-policy-violation";

    public override BrowserReport? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement.Clone();
        var type = GetString(root, "type");
        var uri = GetUri(root, "url");

        if (type is null || uri is null)
        {
            return null;
        }

        return ToLowerInvariant(type) switch
        {
            CspViolationReportType => CreateCspViolationReport(root, type, uri, options),
            CoepReportType => CreateReport<CoepReportBody>(root, type, uri, options, static (body, type, uri) => new CoepViolationReport(body, type, uri)),
            CrashReportType => CreateCrashReport(root, type, uri, options),
            DeprecationReportType => CreateReport<DeprecationReportBody>(root, type, uri, options, static (body, type, uri) => new DeprecationReport(body, type, uri)),
            IntegrityViolationReportType => CreateReport<IntegrityViolationReportBody>(root, type, uri, options, static (body, type, uri) => new IntegrityViolationReport(body, type, uri)),
            InterventionReportType => CreateReport<InterventionReportBody>(root, type, uri, options, static (body, type, uri) => new InterventionReport(body, type, uri)),
            PermissionsPolicyViolationReportType => CreateReport<PermissionsPolicyViolationReportBody>(root, type, uri, options, static (body, type, uri) => new PermissionsPolicyViolationReport(body, type, uri)),
            _ => new GenericBrowserReport(type, uri, root)
        };
    }

    public override void Write(Utf8JsonWriter writer, BrowserReport value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? GetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static Uri? GetUri(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static BrowserReport? CreateReport<TBody>(
        JsonElement root,
        string type,
        Uri uri,
        JsonSerializerOptions options,
        Func<TBody, string, Uri, BrowserReport> factory)
    {
        var body = root.TryGetProperty("body", out var bodyElement)
            ? bodyElement.Deserialize<TBody>(options)
            : default;

        return body is null ? null : factory(body, type, uri);
    }

    private static CspViolationReport? CreateCspViolationReport(JsonElement root, string type, Uri uri, JsonSerializerOptions options)
    {
        var body = DeserializeBody<CspReportBody>(root, options);
        return body is null
            ? null
            : new CspViolationReport(body, type, uri, GetInt32(root, "age"), GetString(root, "user_agent"));
    }

    private static CrashReport? CreateCrashReport(JsonElement root, string type, Uri uri, JsonSerializerOptions options)
    {
        var body = DeserializeBody<CrashReportBody>(root, options);
        return body is null
            ? null
            : new CrashReport(GetInt32(root, "age") ?? 0, type, uri, GetString(root, "user_agent") ?? string.Empty, body);
    }

    private static TBody? DeserializeBody<TBody>(JsonElement root, JsonSerializerOptions options)
    {
        return root.TryGetProperty("body", out var bodyElement)
            ? bodyElement.Deserialize<TBody>(options)
            : default;
    }

    private static string ToLowerInvariant(string value)
    {
        return string.Create(value.Length, value, static (chars, state) =>
        {
            for (var i = 0; i < state.Length; i++)
            {
                chars[i] = char.ToLowerInvariant(state[i]);
            }
        });
    }
}
