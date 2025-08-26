namespace IHFiction.SharedKernel.Sorting;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar Code Smell", "S2326:Unused type parameters should be removed", Justification = "Unique key for service injection")]
public sealed class SortMappingDefinition<TSource, TDestination> : ISortMappingDefinition
{
    public required IEnumerable<SortMapping> Mappings { get; init; }
}
