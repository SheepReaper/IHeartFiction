namespace IHFiction.SharedKernel.Sorting;

public sealed record SortMapping(string SortField, string PropertyName, bool Reverse = false)
{
    public SortMapping(string  fieldAndPropertyName, bool reverse = false) : this(fieldAndPropertyName, fieldAndPropertyName, reverse)
    {
    }
}
