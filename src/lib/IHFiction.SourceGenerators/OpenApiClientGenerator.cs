using Microsoft.CodeAnalysis;

using NJsonSchema.CodeGeneration.CSharp;

using Newtonsoft.Json;

using NSwag;
using NSwag.CodeGeneration.CSharp;

namespace IHFiction.SourceGenerators;

[Generator]
public sealed class OpenApiClientGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor GenerationFailed = new(
        id: "IHFSG001",
        title: "OpenAPI client generation failed",
        messageFormat: "OpenAPI client generation failed for '{0}': {1}",
        category: "IHFiction.SourceGenerators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var openApiFiles = context.AdditionalTextsProvider
            .Where(static file => string.Equals(Path.GetFileName(file.Path), "openapi.json", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) =>
            {
                var source = file.GetText(cancellationToken)?.ToString();
                return string.IsNullOrWhiteSpace(source)
                    ? null
                    : new OpenApiClientInput(file.Path, source!);
            })
            .Where(static input => input is not null)
            .Select(static (input, _) => input!);

        context.RegisterSourceOutput(openApiFiles, static (context, input) => Execute(context, input));
    }

    private static void Execute(SourceProductionContext context, OpenApiClientInput input)
    {
        try
        {
            var document = OpenApiDocument
                .FromJsonAsync(input.Source)
                .GetAwaiter()
                .GetResult();

            var settings = new CSharpClientGeneratorSettings
            {
                ClassName = "FictionApiClient",
                UseBaseUrl = false,
                GenerateOptionalParameters = true,
                GenerateClientInterfaces = true,
                CSharpGeneratorSettings =
                {
                    Namespace = "IHFiction.SharedWeb",
                    JsonLibrary = CSharpJsonLibrary.SystemTextJson
                }
            };

            var typeResolver = new CustomFormatTypeResolver(settings.CSharpGeneratorSettings);
            typeResolver.RegisterSchemaDefinitions(document.Definitions);

            var generator = new CSharpClientGenerator(document, settings, typeResolver);
            var code = generator.GenerateFile();

            context.AddSource("FictionApiClient.g.cs", code);
        }
        catch (InvalidOperationException ex)
        {
            ReportGenerationFailed(context, input, ex);
        }
        catch (IOException ex)
        {
            ReportGenerationFailed(context, input, ex);
        }
        catch (JsonException ex)
        {
            ReportGenerationFailed(context, input, ex);
        }
    }

    private static void ReportGenerationFailed(SourceProductionContext context, OpenApiClientInput input, Exception ex)
    {
        context.ReportDiagnostic(Diagnostic.Create(GenerationFailed, Location.None, input.Path, ex.Message));
    }

    private sealed record OpenApiClientInput(string Path, string Source);
}
