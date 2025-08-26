namespace IHFiction.SharedKernel.DataShaping;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ShapesTypeAttribute(Type type) : ResponseShapingAttribute(type);

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class ShapesTypeAttribute<T>() : ResponseShapingAttribute(typeof(T));
