using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Stories.Domain;

public sealed class WorkRead : DomainUlidEntityWithTimestamp
{
    public Ulid WorkId { get; set; }
    public Work Work { get; set; } = default!;
    public required string ReaderKey { get; set; }
    public bool IsCounted { get; set; }
    public DateTime FirstReadAt { get; set; }
    public DateTime LastReadAt { get; set; }
}
