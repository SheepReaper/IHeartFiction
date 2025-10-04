using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Reflection;

using IHFiction.SharedKernel.Linking;

namespace IHFiction.SharedKernel.DataShaping;

public static class DataShapingService
{
    [SuppressMessage("SonarLint", "S1450", Justification = "This is a thread-safe cache shared across invocations.")]
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesCache = new();

    private static readonly HashSet<string> AlwaysIncluded = ["links"];

    public static ExpandoObject ShapeData<T>(T data, string? fields)
    {
        ArgumentNullException.ThrowIfNull(data);

        // If the root object is Linked<Inner>, flatten it so callers get inner properties at top-level
        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Linked<>))
        {
            var innerType = typeof(T).GetGenericArguments()[0];
            var valueProp = typeof(T).GetProperty("Value");
            var linksProp = typeof(T).GetProperty("Links");

            var valueObj = valueProp?.GetValue(data);

            IDictionary<string, object?> shapedInner = new ExpandoObject();

            if (valueObj is not null)
            {
                var innerProperties = PropertiesCache.GetOrAdd(
                    innerType,
                    t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

                foreach (var innerProperty in innerProperties)
                {
                    shapedInner.Add(innerProperty.Name, innerProperty.GetValue(valueObj));
                }
            }

            // add links property
            if (linksProp is not null)
            {
                shapedInner.Add("links", linksProp.GetValue(data));
            }

            return (ExpandoObject)shapedInner;
        }

        var fieldSet = fields?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        PropertyInfo[] propertyInfos = PropertiesCache.GetOrAdd(
            typeof(T),
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        if (fieldSet.Count != 0)
        {
            fieldSet = [.. fieldSet, .. AlwaysIncluded];
            propertyInfos = [.. propertyInfos.Where(p => fieldSet.Contains(p.Name))];
        }

        IDictionary<string, object?> shapedObject = new ExpandoObject();

        foreach (PropertyInfo property in propertyInfos)
        {
            object? value = null;

            // if property type is any IEnumerable<T> where T is Linked<InnerType>, project each element to ExpandoObject
            // that glues the links property to the inner object properties
            var enumerableInterface = property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? property.PropertyType
                : property.PropertyType.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            bool handled = false;
            if (enumerableInterface is not null)
            {
                var enumElementType = enumerableInterface.GetGenericArguments()[0];
                if (enumElementType.IsGenericType && enumElementType.GetGenericTypeDefinition() == typeof(Linked<>))
                {
                    handled = true;

                    var list = (IEnumerable<object?>?)property.GetValue(data);
                    if (list is null)
                    {
                        value = null;
                    }
                    else
                    {
                        var innerType = enumElementType.GetGenericArguments()[0];
                        var innerProperties = PropertiesCache.GetOrAdd(
                            innerType,
                            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

                        var shapedList = new List<ExpandoObject>();

                        // reflection helpers on the Linked<> wrapper
                        var linkedValueProp = enumElementType.GetProperty("Value");
                        var linksProperty = enumElementType.GetProperty("Links");

                        foreach (var item in list)
                        {
                            if (item is null)
                            {
                                continue;
                            }

                            IDictionary<string, object?> shapedItem = new ExpandoObject();

                            // extract the inner value from the Linked<> wrapper
                            var innerObj = linkedValueProp?.GetValue(item);

                            // add inner properties (if present)
                            if (innerObj is not null)
                            {
                                foreach (var innerProperty in innerProperties)
                                {
                                    shapedItem.Add(innerProperty.Name, innerProperty.GetValue(innerObj));
                                }
                            }
                            else
                            {
                                // if inner object is null, still create the keys with null values
                                foreach (var innerProperty in innerProperties)
                                {
                                    shapedItem.Add(innerProperty.Name, null);
                                }
                            }

                            // add links property from the Linked<> wrapper
                            if (linksProperty is not null)
                            {
                                shapedItem.Add("links", linksProperty.GetValue(item));
                            }

                            shapedList.Add((ExpandoObject)shapedItem);
                        }

                        value = shapedList.AsReadOnly();
                    }
                }
            }

            if (!handled && property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Linked<>))
            {
                var linkedValue = property.GetValue(data);
                if (linkedValue is null)
                {
                    value = null;
                }
                else
                {
                    var innerType = property.PropertyType.GetGenericArguments()[0];
                    var innerProperties = PropertiesCache.GetOrAdd(
                        innerType,
                        t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

                    IDictionary<string, object?> shapedItem = new ExpandoObject();

                    // reflection helpers on the Linked<> wrapper
                    var valueProp = property.PropertyType.GetProperty("Value");
                    var linksProp = property.PropertyType.GetProperty("Links");

                    var innerObj = valueProp?.GetValue(linkedValue);

                    // add inner properties
                    if (innerObj is not null)
                    {
                        foreach (var innerProperty in innerProperties)
                        {
                            shapedItem.Add(innerProperty.Name, innerProperty.GetValue(innerObj));
                        }
                    }
                    else
                    {
                        foreach (var innerProperty in innerProperties)
                        {
                            shapedItem.Add(innerProperty.Name, null);
                        }
                    }

                    // add links property from the Linked<> wrapper
                    if (linksProp is not null)
                    {
                        shapedItem.Add("links", linksProp.GetValue(linkedValue));
                    }

                    value = (ExpandoObject)shapedItem;
                }
            }
            if (!handled && property.PropertyType.IsArray && property.PropertyType.GetElementType() is { } elementType
                && elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(Linked<>))
            {
                var array = (Array?)property.GetValue(data);
                if (array is null)
                {
                    value = null;
                }
                else
                {
                    var innerType = elementType.GetGenericArguments()[0];
                    var innerProperties = PropertiesCache.GetOrAdd(
                        innerType,
                        t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

                    var shapedList = new List<ExpandoObject>();

                    var linkedValueProp = elementType.GetProperty("Value");
                    var linksProperty = elementType.GetProperty("Links");

                    foreach (var item in array)
                    {
                        if (item is null)
                        {
                            continue;
                        }

                        IDictionary<string, object?> shapedItem = new ExpandoObject();

                        var innerObj = linkedValueProp?.GetValue(item);

                        // add inner properties
                        if (innerObj is not null)
                        {
                            foreach (var innerProperty in innerProperties)
                            {
                                shapedItem.Add(innerProperty.Name, innerProperty.GetValue(innerObj));
                            }
                        }
                        else
                        {
                            foreach (var innerProperty in innerProperties)
                            {
                                shapedItem.Add(innerProperty.Name, null);
                            }
                        }

                        // add links property
                        if (linksProperty is not null)
                        {
                            shapedItem.Add("links", linksProperty.GetValue(item));
                        }

                        shapedList.Add((ExpandoObject)shapedItem);
                    }

                    value = shapedList.ToArray();
                }
            }
            if (!handled)
            {
                value = property.GetValue(data);
            }

            shapedObject.Add(property.Name, value);
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
            fieldSet = [.. fieldSet, .. AlwaysIncluded];
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
