namespace IHFiction.SharedKernel.Entities;

public abstract class DomainEntityWithTimestamp<TKey> : DomainEntity<TKey>, ICreatedAt, IUpdatedAt where TKey : struct
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}