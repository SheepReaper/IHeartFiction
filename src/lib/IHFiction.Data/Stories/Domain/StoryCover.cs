using System.Diagnostics.CodeAnalysis;

using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Stories.Domain;

public sealed class StoryCover : DomainUlidEntityWithTimestamp
{
    public Ulid StoryId { get; set; }
    public Story Story { get; set; } = default!;
    public string OriginalFileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long FileSizeBytes { get; set; }
    public string ContentHash { get; set; } = default!;
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Binary cover content is stored directly for EF Core persistence.")]
    public byte[] Content { get; set; } = [];
}