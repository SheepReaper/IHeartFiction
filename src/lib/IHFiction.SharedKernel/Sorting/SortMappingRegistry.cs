using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace IHFiction.SharedKernel.Sorting;

public static class SortMappingRegistry
{
    [SuppressMessage("SonarLint", "S1450", Justification = "This is a thread-safe registry shared across invocations.")]
    private static readonly ConcurrentDictionary<Type, SortMapping[]> Mappings = new();

    public static void Register<T>(SortMapping[] mappings) => Register(typeof(T), mappings);

    public static void Register(Type type, SortMapping[] mappings)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(mappings);

        Mappings[type] = mappings;
    }

    public static SortMapping[] GetMappings<T>() => GetMappings(typeof(T));

    public static SortMapping[] GetMappings(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (Mappings.TryGetValue(type, out var mappings))
        {
            return mappings;
        }

        throw new InvalidOperationException($"No sort mappings registered for type '{type.FullName}'.");
    }

    public static bool TryGetMappings<T>(out SortMapping[] mappings) => TryGetMappings(typeof(T), out mappings);

    public static bool TryGetMappings(Type type, out SortMapping[] mappings)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (Mappings.TryGetValue(type, out var found))
        {
            mappings = found;
            return true;
        }

        mappings = [];
        return false;
    }

    public static bool Validate<T>(ISortingSupport request) => GetMappings<T>().Validate(request);

    public static bool Validate<T>(string? sortParam = null) => GetMappings<T>().Validate(sortParam);
}
