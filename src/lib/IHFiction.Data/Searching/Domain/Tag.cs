using IHFiction.Data.Stories.Domain;
using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Searching.Domain;

public abstract class Tag : DomainUlidEntityWithTimestamp
{
    public string Category { get; set; } = default!;
    public string? Subcategory { get; set; }
    public string Value { get; set; } = default!;

    private ICollection<Work>? _works;
    public ICollection<Work> Works => _works ??= [];

    public override string ToString() => Subcategory is null ? $"{Category}:{Value}" : $"{Category}:{Subcategory}:{Value}";
}