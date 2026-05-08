using IHFiction.ApiClientGenerator;

using NJsonSchema.CodeGeneration.CSharp;

using NSwag;
using NSwag.CodeGeneration.CSharp;

var input = args.Length > 0 ? args[0] : throw new ArgumentException("Input file path is required as the first argument.");
var output = args.Length > 1 ? args[1] : throw new ArgumentException("Output file path is required as the second argument.");

var document = await OpenApiDocument.FromFileAsync(input);

CSharpClientGeneratorSettings settings = new()
{
    ClassName = "FictionApiClient",
    UseBaseUrl = false,
    GenerateOptionalParameters = true,
    CSharpGeneratorSettings =
    {
        Namespace = "IHFiction.SharedWeb",
        JsonLibrary = CSharpJsonLibrary.SystemTextJson
    }
};

CustomFormatTypeResolver typeResolver = new(settings.CSharpGeneratorSettings);

typeResolver.RegisterSchemaDefinitions(document.Definitions);

CSharpClientGenerator generator = new(document, settings, typeResolver);

var code = generator.GenerateFile();

await File.WriteAllTextAsync(output, code);
