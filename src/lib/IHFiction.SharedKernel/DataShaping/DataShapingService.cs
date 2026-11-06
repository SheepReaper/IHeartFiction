using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Pagination;

namespace IHFiction.SharedKernel.DataShaping;

public static class DataShapingService
{
    [SuppressMessage("SonarLint", "S1450", Justification = "This is a thread-safe cache shared across invocations.")]
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesCache = new();

    [SuppressMessage("SonarLint", "S1450", Justification = "This is a thread-safe registry shared across invocations.")]
    private static readonly ConcurrentDictionary<Type, IReadOnlySet<string>> RegisteredPropertyNames = new();

    [SuppressMessage("SonarLint", "S1450", Justification = "This is a thread-safe registry shared across invocations.")]
    private static readonly ConcurrentDictionary<Type, Func<object, HashSet<string>, ExpandoObject>> ObjectShapers = new();

    private static readonly HashSet<string> AlwaysIncluded = ["links"];

    /// <summary>
    /// Registers a compile-time source-generated property allowlist and property writer for <typeparamref name="TItem"/>,
    /// along with strongly-typed shapers for <see cref="Linked{TItem}"/> and <see cref="LinkedPagedCollection{TItem}"/>.
    /// </summary>
    public static void RegisterShaper<TItem>(
        string[] propertyNames,
        Action<TItem, IReadOnlySet<string>?, IDictionary<string, object?>> writeProperties)
    {
        ArgumentNullException.ThrowIfNull(propertyNames);
        ArgumentNullException.ThrowIfNull(writeProperties);

        var propertyNameSet = new HashSet<string>(propertyNames, StringComparer.OrdinalIgnoreCase);
        RegisteredPropertyNames[typeof(TItem)] = propertyNameSet;

        ObjectShapers[typeof(TItem)] = (obj, fieldSet) =>
        {
            IDictionary<string, object?> shaped = new ExpandoObject();
            HashSet<string>? activeFields = fieldSet.Count > 0 ? fieldSet : null;
            writeProperties((TItem)obj, activeFields, shaped);
            return (ExpandoObject)shaped;
        };

        ObjectShapers[typeof(Linked<TItem>)] = (obj, fieldSet) =>
        {
            var linked = (Linked<TItem>)obj;
            IDictionary<string, object?> shaped = new ExpandoObject();
            HashSet<string>? activeFields = fieldSet.Count > 0 ? fieldSet : null;

            if (linked.Value is not null)
            {
                writeProperties(linked.Value, activeFields, shaped);
            }

            shaped.Add("links", linked.Links);
            return (ExpandoObject)shaped;
        };

        ObjectShapers[typeof(LinkedPagedCollection<TItem>)] = (obj, fieldSet) =>
        {
            var page = (LinkedPagedCollection<TItem>)obj;
            IDictionary<string, object?> shaped = new ExpandoObject();
            HashSet<string>? activeFields = fieldSet.Count > 0 ? fieldSet : null;

            if (page.Data is null)
            {
                shaped.Add(nameof(LinkedPagedCollection<>.Data), null);
            }
            else
            {
                var shapedList = new List<ExpandoObject>();
                foreach (var item in page.Data)
                {
                    if (item is null)
                    {
                        continue;
                    }

                    IDictionary<string, object?> shapedItem = new ExpandoObject();
                    if (item.Value is not null)
                    {
                        writeProperties(item.Value, activeFields, shapedItem);
                    }
                    else
                    {
                        foreach (var propName in propertyNames.Where(propName => activeFields is null || activeFields.Contains(propName)))
                        {
                            shapedItem.Add(propName, null);
                        }
                    }

                    shapedItem.Add("links", item.Links);
                    shapedList.Add((ExpandoObject)shapedItem);
                }

                shaped.Add(nameof(LinkedPagedCollection<>.Data), shapedList.AsReadOnly());
            }

            shaped.Add(nameof(LinkedPagedCollection<>.TotalCount), page.TotalCount);
            shaped.Add(nameof(LinkedPagedCollection<>.CurrentPage), page.CurrentPage);
            shaped.Add(nameof(LinkedPagedCollection<>.PageSize), page.PageSize);
            shaped.Add(nameof(LinkedPagedCollection<>.TotalPages), page.TotalPages);
            shaped.Add(nameof(LinkedPagedCollection<>.Links), page.Links);
            return (ExpandoObject)shaped;
        };
    }

    public static bool HasRegisteredShaper<T>() => HasRegisteredShaper(typeof(T));

    public static bool HasRegisteredShaper(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return RegisteredPropertyNames.ContainsKey(type) || ObjectShapers.ContainsKey(type);
    }

    public static ExpandoObject ShapeObject(object data, string? fields) => ShapeData(data, fields);

    public static ExpandoObject ShapeData<T>(T data, string? fields)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data is ExpandoObject alreadyShaped)
        {
            return alreadyShaped;
        }

        var fieldSet = fields?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        var runtimeType = data.GetType();
        if (ObjectShapers.TryGetValue(runtimeType, out var registeredShaper))
        {
            return registeredShaper(data, fieldSet);
        }

        // If the root object is Linked<Inner>, flatten it so callers get inner properties at top-level
        if (runtimeType.IsGenericType && runtimeType.GetGenericTypeDefinition() == typeof(Linked<>))
        {
            var innerType = runtimeType.GetGenericArguments()[0];
            var valueProp = runtimeType.GetProperty("Value");
            var linksProp = runtimeType.GetProperty("Links");

            var valueObj = valueProp?.GetValue(data);

            IDictionary<string, object?> shapedInner = new ExpandoObject();

            if (valueObj is not null)
            {
                var innerProperties = PropertiesCache.GetOrAdd(
                    innerType,
                    t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

                if (fieldSet.Count != 0)
                {
                    innerProperties = [.. innerProperties.Where(p => fieldSet.Contains(p.Name))];
                }

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

        PropertyInfo[] propertyInfos = PropertiesCache.GetOrAdd(
            runtimeType,
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        var shapesLinkedCollection = propertyInfos.Any(property =>
        {
            var enumerable = property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? property.PropertyType
                : property.PropertyType.GetInterfaces().FirstOrDefault(candidate =>
                    candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            var elementType = enumerable?.GetGenericArguments()[0];
            return elementType is { IsGenericType: true } && elementType.GetGenericTypeDefinition() == typeof(Linked<>);
        });

        if (fieldSet.Count != 0 && !shapesLinkedCollection)
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
                        if (fieldSet.Count != 0)
                        {
                            innerProperties = [.. innerProperties.Where(innerProperty => fieldSet.Contains(innerProperty.Name))];
                        }

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

    /// <summary>
    /// Prunes an <see cref="IQueryable{T}"/> projection so that unrequested properties and subqueries
    /// are replaced with default constants before database query execution, while preserving primary
    /// identifier properties needed for hypermedia link generation and any properties referenced by
    /// post-projection operators.
    /// </summary>
    public static IQueryable<T> ShapeProjection<T>(IQueryable<T> query, string? fields)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(fields))
        {
            return query;
        }

        var fieldSet = fields
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (fieldSet.Count == 0)
        {
            return query;
        }

        var postProjectionCollector = new PostProjectionMemberCollector(typeof(T));
        postProjectionCollector.Visit(query.Expression);
        foreach (var memberName in postProjectionCollector.AccessedMembers)
        {
            fieldSet.Add(memberName);
        }

        var rewriter = new ProjectionPruningVisitor(typeof(T), fieldSet);
        var rewritten = rewriter.Visit(query.Expression);

        if (rewriter.HasRewrittenProjection)
        {
            return query.Provider.CreateQuery<T>(rewritten);
        }

        return TryAppendEntityMemberInitProjection(query, fieldSet);
    }

    private static IQueryable<T> TryAppendEntityMemberInitProjection<T>(IQueryable<T> query, HashSet<string> fieldSet)
    {
        var targetType = typeof(T);
        var parameterlessCtor = targetType.GetConstructor(Type.EmptyTypes);
        if (parameterlessCtor is null)
        {
            return query;
        }

        var writableProps = PropertiesCache.GetOrAdd(
                targetType,
                t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(p => p.CanRead && p.CanWrite)
            .ToArray();

        if (writableProps.Length == 0)
        {
            return query;
        }

        var keptProps = writableProps
            .Where((p, index) => ShouldKeepMember(p.Name, index, p.PropertyType, fieldSet))
            .ToArray();

        if (keptProps.Length == 0 || keptProps.Length == writableProps.Length)
        {
            return query;
        }

        var param = Expression.Parameter(targetType, "e");
        var bindings = keptProps
            .Select(p => (MemberBinding)Expression.Bind(p, Expression.Property(param, p)))
            .ToArray();

        var selector = Expression.Lambda<Func<T, T>>(
            Expression.MemberInit(Expression.New(parameterlessCtor), bindings),
            param);

        return query.Select(selector);
    }

    private static bool ShouldKeepMember(string memberName, int parameterIndex, Type memberType, HashSet<string> fieldSet)
    {
        if (fieldSet.Contains(memberName))
        {
            return true;
        }

        if (memberName.Equals("Id", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (parameterIndex == 0 && memberName.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var underlyingType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        if (underlyingType == typeof(Ulid))
        {
            return true;
        }

        return false;
    }

    private sealed class PostProjectionMemberCollector(Type targetType) : ExpressionVisitor
    {
        private bool _hasFoundProjectionSelect;

        public HashSet<string> AccessedMembers { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable) &&
                node.Method.Name == nameof(Queryable.Select) &&
                node.Method.GetGenericArguments() is [_, var resultType] &&
                resultType == targetType)
            {
                _hasFoundProjectionSelect = true;
                return node;
            }

            // Visit outer arguments (such as OrderBy/Where lambdas) before recursing into the source queryable
            for (var i = 1; i < node.Arguments.Count; i++)
            {
                Visit(node.Arguments[i]);
            }

            if (node.Arguments.Count > 0)
            {
                Visit(node.Arguments[0]);
            }

            if (!_hasFoundProjectionSelect)
            {
                AccessedMembers.Clear();
            }

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression?.Type == targetType)
            {
                AccessedMembers.Add(node.Member.Name);
            }

            return base.VisitMember(node);
        }
    }

    private sealed class ProjectionPruningVisitor(Type targetType, HashSet<string> fieldSet) : ExpressionVisitor
    {
        public bool HasRewrittenProjection { get; private set; }

        protected override Expression VisitNew(NewExpression node)
        {
            if (node.Type != targetType || node.Constructor is null)
            {
                return base.VisitNew(node);
            }

            var parameters = node.Constructor.GetParameters();
            if (parameters.Length == 0 || parameters.Length != node.Arguments.Count)
            {
                return base.VisitNew(node);
            }

            var newArgs = new Expression[node.Arguments.Count];
            var changed = false;

            for (var i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var paramName = param.Name ?? string.Empty;

                if (ShouldKeepMember(paramName, i, param.ParameterType, fieldSet) ||
                    !CanReplaceWithSqlConstant(param.ParameterType))
                {
                    newArgs[i] = node.Arguments[i];
                }
                else
                {
                    newArgs[i] = CreateDefaultConstant(param.ParameterType);
                    changed = true;
                }
            }

            HasRewrittenProjection = true;
            return changed ? node.Update(newArgs) : node;
        }

        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            if (node.Type != targetType)
            {
                return base.VisitMemberInit(node);
            }

            var newExpr = (NewExpression)VisitNew(node.NewExpression);
            var keptBindings = new List<MemberBinding>(node.Bindings.Count);
            var changed = !ReferenceEquals(newExpr, node.NewExpression);

            for (var i = 0; i < node.Bindings.Count; i++)
            {
                var binding = node.Bindings[i];
                var memberType = binding.Member switch
                {
                    PropertyInfo prop => prop.PropertyType,
                    FieldInfo field => field.FieldType,
                    _ => typeof(object)
                };

                if (ShouldKeepMember(binding.Member.Name, i, memberType, fieldSet))
                {
                    keptBindings.Add(binding);
                }
                else
                {
                    changed = true;
                }
            }

            HasRewrittenProjection = true;
            return changed ? node.Update(newExpr, keptBindings) : node;
        }

        private static bool CanReplaceWithSqlConstant(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            return underlying == typeof(string) ||
                   underlying == typeof(bool) ||
                   underlying == typeof(int) ||
                   underlying == typeof(long) ||
                   underlying == typeof(short) ||
                   underlying == typeof(byte) ||
                   underlying == typeof(decimal) ||
                   underlying == typeof(double) ||
                   underlying == typeof(float) ||
                   underlying == typeof(Guid) ||
                   underlying == typeof(DateTime) ||
                   underlying == typeof(DateTimeOffset);
        }

        private static ConstantExpression CreateDefaultConstant(Type type)
        {
            if (type == typeof(string))
            {
                return Expression.Constant(string.Empty, typeof(string));
            }

            if (type == typeof(DateTime))
            {
                return Expression.Constant(DateTime.UnixEpoch, typeof(DateTime));
            }

            if (type == typeof(DateTimeOffset))
            {
                return Expression.Constant(DateTimeOffset.UnixEpoch, typeof(DateTimeOffset));
            }

            var defaultValue = type.IsValueType ? Activator.CreateInstance(type) : null;
            return Expression.Constant(defaultValue, type);
        }
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

        if (fieldSet.Count == 0)
        {
            errors = new ValidationResult("Fields must include at least one field.");
            return false;
        }

        if (RegisteredPropertyNames.TryGetValue(type, out var validNames))
        {
            string[] invalidRegisteredFields = [.. fieldSet.Where(field => !validNames.Contains(field))];
            if (invalidRegisteredFields.Length > 0)
            {
                errors = new ValidationResult($"Data shaping field{(invalidRegisteredFields.Length > 1 ? "s" : "")}: {string.Join(", ", invalidRegisteredFields)} {(invalidRegisteredFields.Length > 1 ? "are" : "is")} not valid.");
                return false;
            }

            return true;
        }

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
