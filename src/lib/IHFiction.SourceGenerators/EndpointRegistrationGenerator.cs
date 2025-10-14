using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IHFiction.SourceGenerators;

[Generator]
public class EndpointRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes that implement IEndpoint
        var endpointClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsClassDeclaration(s),
                transform: static (ctx, _) => GetClassDeclaration(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!)
            .Collect();

        // Find all classes that implement IUseCase
        var useCaseClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsClassDeclaration(s),
                transform: static (ctx, _) => GetUseCaseDeclaration(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!)
            .Collect();

        // Combine both collections and generate the registration code
        var combined = endpointClasses
            .Combine(useCaseClasses)
            .Select(static (x, _) => (Endpoints: x.Left, UseCases: x.Right));

        context.RegisterSourceOutput(combined, static (spc, source) => Execute(spc, source.Endpoints, source.UseCases));
    }

    private static bool IsClassDeclaration(SyntaxNode node)
    {
        // Be very specific - only look for nested classes named "Endpoint" or top-level classes with interfaces
        if (node is not ClassDeclarationSyntax { BaseList: not null } cls)
            return false;

        // Check if it's a nested class named "Endpoint" (our endpoint pattern)
        if (cls.Identifier.ValueText == "Endpoint" && cls.Parent is ClassDeclarationSyntax)
            return true;

        // Check if it's a top-level class that might implement IUseCase
        if (cls.Parent is not ClassDeclarationSyntax)
            return true;

        return false;
    }

    private static ClassInfo? GetClassDeclaration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (classDeclaration.BaseList is null)
            return null;

        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

        if (classSymbol is null)
            return null;

        // Skip abstract or interface classes
        if (classSymbol.IsAbstract || classSymbol.TypeKind == TypeKind.Interface)
            return null;

        // Only process classes in the IHFiction.FictionApi namespace
        var containingNamespace = classSymbol.ContainingNamespace?.ToDisplayString();
        if (containingNamespace is null || !containingNamespace.StartsWith("IHFiction.FictionApi", StringComparison.Ordinal))
            return null;

        // Check if it implements IEndpoint
        var implementsIEndpoint = classSymbol.AllInterfaces
            .Any(i => i.Name == "IEndpoint" && i.ContainingNamespace?.ToDisplayString() == "IHFiction.FictionApi.Infrastructure");

        if (!implementsIEndpoint)
            return null;

        // Ensure this is a nested class named "Endpoint"
        if (classSymbol.Name != "Endpoint" || classSymbol.ContainingType is null)
            return null;

        return new ClassInfo(
            classSymbol.Name,
            classSymbol.ToDisplayString(),
            containingNamespace
        );
    }

    private static UseCaseInfo? GetUseCaseDeclaration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (classDeclaration.BaseList is null)
            return null;

        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

        if (classSymbol is null)
            return null;

        // Skip abstract or interface classes
        if (classSymbol.IsAbstract || classSymbol.TypeKind == TypeKind.Interface)
            return null;

        // Only process classes in the IHFiction.FictionApi namespace
        var containingNamespace = classSymbol.ContainingNamespace?.ToDisplayString();
        if (containingNamespace is null || !containingNamespace.StartsWith("IHFiction.FictionApi", StringComparison.Ordinal))
            return null;

        // Check if it implements IUseCase
        var implementsIUseCase = classSymbol.AllInterfaces
            .Any(i => i.Name == "IUseCase" && i.ContainingNamespace?.ToDisplayString() == "IHFiction.FictionApi.Infrastructure");

        if (!implementsIUseCase)
            return null;

        // Skip nested classes (UseCase classes should be top-level)
        if (classSymbol.ContainingType is not null)
            return null;

        return new UseCaseInfo(
            classSymbol.Name,
            classSymbol.ToDisplayString(),
            containingNamespace
        );
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<ClassInfo> endpoints, ImmutableArray<UseCaseInfo> useCases)
    {
        // Always generate the file to test if source generator is running
        var source = GenerateRegistrationCode(endpoints, useCases);
        context.AddSource("EndpointRegistration.g.cs", source);
    }

    private static string GenerateRegistrationCode(ImmutableArray<ClassInfo> endpoints, ImmutableArray<UseCaseInfo> useCases)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        sb.AppendLine("using IHFiction.FictionApi.Infrastructure;");
        sb.AppendLine();

        // Add using statements for all namespaces
        var allNamespaces = endpoints.Select(e => e.Namespace)
            .Concat(useCases.Select(u => u.Namespace))
            .Where(ns => !string.IsNullOrEmpty(ns))
            .Distinct()
            .OrderBy(ns => ns);

        foreach (var ns in allNamespaces)
        {
            sb.AppendLine($"using {ns};");
        }
        sb.AppendLine();

        sb.AppendLine("namespace IHFiction.FictionApi.Extensions;");
        sb.AppendLine();
        sb.AppendLine("internal static partial class EndpointExtensions");
        sb.AppendLine("{");

        // Generate AddEndpoints method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all endpoint implementations with the dependency injection container.");
        sb.AppendLine("    /// This method is generated at compile-time and is AOT compatible.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"services\">The service collection to add endpoints to.</param>");
        sb.AppendLine("    /// <returns>The service collection for method chaining.</returns>");
        sb.AppendLine("    static partial void AddGeneratedEndpoints(this IServiceCollection services)");
        sb.AppendLine("    {");

        foreach (var endpoint in endpoints.OrderBy(e => e.FullName))
        {
            sb.AppendLine($"        services.TryAddEnumerable(ServiceDescriptor.Transient<IEndpoint, {endpoint.FullName}>());");
        }

        // sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate AddUseCases method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all use case implementations with the dependency injection container.");
        sb.AppendLine("    /// This method is generated at compile-time and is AOT compatible.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"services\">The service collection to add use cases to.</param>");
        sb.AppendLine("    /// <returns>The service collection for method chaining.</returns>");
        sb.AppendLine("    static partial void AddGeneratedUseCases(this IServiceCollection services)");
        sb.AppendLine("    {");

        foreach (var useCase in useCases.OrderBy(u => u.FullName))
        {
            sb.AppendLine($"        services.AddScoped<{useCase.FullName}>();");
        }

        // sb.AppendLine("        return services;");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    private sealed record ClassInfo(string Name, string FullName, string Namespace);
    private sealed record UseCaseInfo(string Name, string FullName, string Namespace);
}