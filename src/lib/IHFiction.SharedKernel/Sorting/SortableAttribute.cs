namespace IHFiction.SharedKernel.Sorting;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field, AllowMultiple = false)]
public sealed class SortableAttribute(string? targetProperty = null, bool reverse = false) : Attribute
{
    public string? TargetProperty { get; } = targetProperty;
    public bool Reverse { get; } = reverse;
}
