namespace IHFiction.SharedKernel.Sorting;

public sealed class SortMappingProvider(IEnumerable<ISortMappingDefinition> sortMappingDefinitions)
{
    public SortMapping[] GetMappings<TSource, TDestination>()
    {
        var sortMappingDefinition = sortMappingDefinitions
            .OfType<SortMappingDefinition<TSource, TDestination>>()
            .FirstOrDefault();

        return sortMappingDefinition is null
            ? throw new InvalidOperationException($"No sort mapping definition found for {typeof(TSource).Name} and {typeof(TDestination).Name}")
            : [.. sortMappingDefinition.Mappings];
    }
}