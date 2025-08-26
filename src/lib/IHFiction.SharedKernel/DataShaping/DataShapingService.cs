using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Reflection;

namespace IHFiction.SharedKernel.DataShaping;

public static class DataShapingService
{
    [SuppressMessage("SonarLint", "S1450", Justification = "This is a thread-safe cache shared across invocations.")]
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesCache = new();

    private static readonly HashSet<string> AlwaysIncluded = ["links"];

    public static ExpandoObject ShapeData<T>(T data, string? fields)
    {
        ArgumentNullException.ThrowIfNull(data);

        var fieldSet = fields?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        PropertyInfo[] propertyInfos = PropertiesCache.GetOrAdd(
            typeof(T),
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        if (fieldSet.Count != 0)
        {
            fieldSet = [.. fieldSet, ..AlwaysIncluded];
            propertyInfos = [.. propertyInfos.Where(p => fieldSet.Contains(p.Name))];
        }

        IDictionary<string, object?> shapedObject = new ExpandoObject();

        foreach (PropertyInfo property in propertyInfos)
        {
            shapedObject.Add(property.Name, property.GetValue(data));
        }

        return (ExpandoObject)shapedObject;
    }

    public static ReadOnlyCollection<ExpandoObject> ShapeData<T>(IEnumerable<T> data, string? fields)
    {
        ArgumentNullException.ThrowIfNull(data);

        var fieldSet = fields?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        PropertyInfo[] propertyInfos = PropertiesCache.GetOrAdd(
            typeof(T),
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        if (fieldSet.Count != 0)
        {
            fieldSet = [.. fieldSet, ..AlwaysIncluded];
            propertyInfos = [.. propertyInfos.Where(p => fieldSet.Contains(p.Name))];
        }

        List<ExpandoObject> shapedData = [];

        foreach (T entity in data)
        {
            IDictionary<string, object?> shapedObject = new ExpandoObject();

            foreach (PropertyInfo property in propertyInfos)
            {
                shapedObject.Add(property.Name, property.GetValue(entity));
            }

            shapedData.Add((ExpandoObject)shapedObject);
        }

        return shapedData.AsReadOnly();
    }

    public static void EnsureValid<T>(IDataShapingSupport request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureValid<T>(request.Fields);
    }

    public static void EnsureValid<T>(string? fields)
    {
        if (!TryValidate<T>(fields, out var errors))
        {
            throw new ValidationException(errors, null, null);
        }
    }

    public static bool Validate<T>(IDataShapingSupport request) => TryValidate<T>(request, out _);

    public static bool Validate<T>(string? fields) => TryValidate<T>(fields, out _);

    public static bool TryValidate<T>(IDataShapingSupport request, [NotNullWhen(false)] out ValidationResult? errors)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TryValidate<T>(request.Fields, out errors);
    }

    public static bool TryValidate<T>(string? fields, [NotNullWhen(false)] out ValidationResult? errors) =>
        TryValidate(typeof(T), fields, out errors);

    public static bool TryValidate(Type type, object? fields, [NotNullWhen(false)] out ValidationResult? errors)
    {
        static bool InvalidFieldsParam(out ValidationResult? errors)
        {
            errors = new ValidationResult("Fields must be a string or null.");
            return false;
        }

        errors = null;

        return fields switch
        {
            null => true,
            string s => TryValidate(type, s, out errors),
            _ => InvalidFieldsParam(out errors)
        };
    }

    public static bool TryValidate(Type type, string? fields, [NotNullWhen(false)] out ValidationResult? errors)
    {
        errors = null;

        if (string.IsNullOrWhiteSpace(fields))
        {
            return true;
        }

        var fieldSet = fields
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        PropertyInfo[] propertyInfos = PropertiesCache.GetOrAdd(
            type,
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        string[] invalidFields = [.. fieldSet.Where(field => !propertyInfos.Any(p => p.Name.Equals(field, StringComparison.OrdinalIgnoreCase)))];

        if (invalidFields.Length > 0)
        {
            errors = new ValidationResult($"Data shaping field{(invalidFields.Length > 1 ? "s" : "")}: {string.Join(", ", invalidFields)} {(invalidFields.Length > 1 ? "are" : "is")} not valid.");
            return false;
        }

        return true;
    }
}
