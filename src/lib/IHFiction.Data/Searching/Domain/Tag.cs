using IHFiction.Data.Stories.Domain;
using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Searching.Domain;

public abstract class Tag : DomainUlidEntityWithTimestamp
{
    public required string Category { get; set; }
    public string? Subcategory { get; set; }
    public required string Value { get; set; }

    private ICollection<Work>? _works;
    public ICollection<Work> Works => _works ??= [];

    public override string ToString() => Subcategory is null ? $"{Category}:{Value}" : $"{Category}:{Subcategory}:{Value}";
}