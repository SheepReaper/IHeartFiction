using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IHFiction.SourceGenerators;

[Generator]
public sealed class SortingAndShapingGenerator : IIncrementalGenerator
{
    private const string ShapesTypeAttributeMetadataMask = "IHFiction.SharedKernel.DataShaping.ShapesTypeAttribute";
    private const string SortableAttributeFullName = "IHFiction.SharedKernel.Sorting.SortableAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsCandidateTypeDeclaration(node),
                transform: static (ctx, _) => InspectTypeDeclaration(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!)
            .Collect();

        context.RegisterImplementationSourceOutput(candidates, static (spc, items) => Execute(spc, items));
    }

    private static bool IsCandidateTypeDeclaration(SyntaxNode node)
    {
        return node is TypeDeclarationSyntax and (ClassDeclarationSyntax or RecordDeclarationSyntax or StructDeclarationSyntax);
    }

    private static DiscoveredTypeMetadata? InspectTypeDeclaration(GeneratorSyntaxContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        if (typeSymbol.IsAbstract || typeSymbol.TypeKind == TypeKind.Interface || typeSymbol.IsGenericType)
        {
            return null;
        }

        var containingNamespace = typeSymbol.ContainingNamespace?.ToDisplayString();
        if (containingNamespace is null || !containingNamespace.StartsWith("IHFiction.FictionApi", StringComparison.Ordinal))
        {
            return null;
        }

        var shapedTargets = ExtractShapedTargetTypes(typeSymbol);
        var sortableMappings = ExtractSortableMappings(typeSymbol);

        if (shapedTargets.Count == 0 && sortableMappings.Count == 0)
        {
            return null;
        }

        string? sortableOwnerTypeName = sortableMappings.Count > 0
            ? typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            : null;

        return new DiscoveredTypeMetadata(
            shapedTargets.ToImmutableArray(),
            sortableOwnerTypeName,
            sortableMappings.ToImmutableArray());
    }

    private static List<ShapedTypeInfo> ExtractShapedTargetTypes(INamedTypeSymbol typeSymbol)
    {
        var results = new List<ShapedTypeInfo>();

        foreach (var member in typeSymbol.GetMembers())
        {
            foreach (var attribute in member.GetAttributes())
            {
                var targetType = TryGetShapedTargetType(attribute);
                if (targetType is not null)
                {
                    results.Add(BuildShapedTypeInfo(targetType));
                }
            }
        }

        foreach (var ctor in typeSymbol.InstanceConstructors)
        {
            foreach (var param in ctor.Parameters)
            {
                foreach (var attribute in param.GetAttributes())
                {
                    var targetType = TryGetShapedTargetType(attribute);
                    if (targetType is not null)
                    {
                        results.Add(BuildShapedTypeInfo(targetType));
                    }
                }
            }
        }

        return results;
    }

    private static INamedTypeSymbol? TryGetShapedTargetType(AttributeData attribute)
    {
        var attrClass = attribute.AttributeClass;
        if (attrClass is null)
        {
            return null;
        }

        var display = attrClass.OriginalDefinition.ToDisplayString();
        if (!display.StartsWith(ShapesTypeAttributeMetadataMask, StringComparison.Ordinal))
        {
            return null;
        }

        if (attrClass.IsGenericType && attrClass.TypeArguments.Length == 1 && attrClass.TypeArguments[0] is INamedTypeSymbol genericTarget)
        {
            return genericTarget;
        }

        if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is INamedTypeSymbol typeofTarget)
        {
            return typeofTarget;
        }

        return null;
    }

    private static ShapedTypeInfo BuildShapedTypeInfo(INamedTypeSymbol targetType)
    {
        var fullName = targetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var properties = GetPublicInstanceProperties(targetType)
            .Select(p => p.Name)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();

        return new ShapedTypeInfo(fullName, properties);
    }

    private static IEnumerable<IPropertySymbol> GetPublicInstanceProperties(INamedTypeSymbol typeSymbol)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = typeSymbol;

        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (member.DeclaredAccessibility == Accessibility.Public &&
                    !member.IsStatic &&
                    !member.IsIndexer &&
                    member.GetMethod is not null &&
                    seen.Add(member.Name))
                {
                    yield return member;
                }
            }

            current = current.BaseType;
        }
    }

    private static List<SortablePropertyInfo> ExtractSortableMappings(INamedTypeSymbol typeSymbol)
    {
        var mappings = new List<SortablePropertyInfo>();
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Check public instance properties for [Sortable]
        foreach (var prop in typeSymbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (prop.DeclaredAccessibility != Accessibility.Public || prop.IsStatic)
            {
                continue;
            }

            var sortableAttr = prop.GetAttributes().FirstOrDefault(IsSortableAttribute);
            if (sortableAttr is not null && seenFields.Add(prop.Name))
            {
                mappings.Add(ParseSortableAttribute(prop.Name, sortableAttr));
            }
        }

        // 2. Check record primary constructor parameters for [Sortable] (in case [Sortable] was applied without `property:`)
        foreach (var ctor in typeSymbol.InstanceConstructors)
        {
            foreach (var param in ctor.Parameters)
            {
                var sortableAttr = param.GetAttributes().FirstOrDefault(IsSortableAttribute);
                if (sortableAttr is not null && seenFields.Add(param.Name))
                {
                    mappings.Add(ParseSortableAttribute(param.Name, sortableAttr));
                }
            }
        }

        return mappings;
    }

    private static bool IsSortableAttribute(AttributeData attribute)
    {
        return attribute.AttributeClass?.ToDisplayString() == SortableAttributeFullName;
    }

    private static SortablePropertyInfo ParseSortableAttribute(string memberName, AttributeData attribute)
    {
        string? targetProperty = null;
        bool reverse = false;

        if (attribute.ConstructorArguments.Length >= 1 && attribute.ConstructorArguments[0].Value is string ctorTarget && !string.IsNullOrWhiteSpace(ctorTarget))
        {
            targetProperty = ctorTarget;
        }

        if (attribute.ConstructorArguments.Length >= 2 && attribute.ConstructorArguments[1].Value is bool ctorReverse)
        {
            reverse = ctorReverse;
        }

        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == "TargetProperty" && namedArg.Value.Value is string namedTarget && !string.IsNullOrWhiteSpace(namedTarget))
            {
                targetProperty = namedTarget;
            }
            else if (namedArg.Key == "Reverse" && namedArg.Value.Value is bool namedReverse)
            {
                reverse = namedReverse;
            }
        }

        return new SortablePropertyInfo(
            SortField: memberName,
            PropertyName: targetProperty ?? memberName,
            Reverse: reverse);
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<DiscoveredTypeMetadata> discovered)
    {
        if (discovered.IsEmpty)
        {
            return;
        }

        var shapedTypes = discovered
            .SelectMany(d => d.ShapedTypes)
            .GroupBy(s => s.FullTypeName, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(s => s.FullTypeName, StringComparer.Ordinal)
            .ToList();

        var sortableTypes = discovered
            .Where(d => d.SortableOwnerTypeName is not null && !d.SortableProperties.IsEmpty)
            .GroupBy(d => d.SortableOwnerTypeName!, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(d => d.SortableOwnerTypeName, StringComparer.Ordinal)
            .ToList();

        if (shapedTypes.Count == 0 && sortableTypes.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using IHFiction.SharedKernel.DataShaping;");
        sb.AppendLine("using IHFiction.SharedKernel.Sorting;");
        sb.AppendLine();
        sb.AppendLine("namespace IHFiction.FictionApi.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal static class SortingAndShapingInitializer");
        sb.AppendLine("{");
        sb.AppendLine("    [ModuleInitializer]");
        sb.AppendLine("    internal static void Initialize()");
        sb.AppendLine("    {");

        foreach (var shaped in shapedTypes)
        {
            var propsLiteral = string.Join(", ", shaped.PropertyNames.Select(p => $"\"{p}\""));
            sb.AppendLine($"        DataShapingService.RegisterShaper<{shaped.FullTypeName}>(");
            sb.AppendLine($"            new string[] {{ {propsLiteral} }},");
            sb.AppendLine("            static (source, activeFields, target) =>");
            sb.AppendLine("            {");

            foreach (var prop in shaped.PropertyNames)
            {
                sb.AppendLine($"                if (activeFields is null || activeFields.Contains(\"{prop}\"))");
                sb.AppendLine("                {");
                sb.AppendLine($"                    target[\"{prop}\"] = source.{prop};");
                sb.AppendLine("                }");
            }

            sb.AppendLine("            });");
            sb.AppendLine();
        }

        foreach (var sortable in sortableTypes)
        {
            sb.AppendLine($"        SortMappingRegistry.Register<{sortable.SortableOwnerTypeName}>(");
            sb.AppendLine("            new SortMapping[]");
            sb.AppendLine("            {");

            foreach (var mapping in sortable.SortableProperties)
            {
                var reverseLiteral = mapping.Reverse ? "true" : "false";
                sb.AppendLine($"                new SortMapping(\"{mapping.SortField}\", \"{mapping.PropertyName}\", {reverseLiteral}),");
            }

            sb.AppendLine("            });");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("SortingAndShapingRegistration.g.cs", sb.ToString());
    }

    private sealed record DiscoveredTypeMetadata(
        ImmutableArray<ShapedTypeInfo> ShapedTypes,
        string? SortableOwnerTypeName,
        ImmutableArray<SortablePropertyInfo> SortableProperties);

    private sealed record ShapedTypeInfo(
        string FullTypeName,
        ImmutableArray<string> PropertyNames);

    private sealed record SortablePropertyInfo(
        string SortField,
        string PropertyName,
        bool Reverse);
}
