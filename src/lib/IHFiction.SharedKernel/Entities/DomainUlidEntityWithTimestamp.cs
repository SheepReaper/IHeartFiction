namespace IHFiction.SharedKernel.Entities;

public abstract class DomainUlidEntityWithTimestamp : DomainUlidEntity, IReadCreatedAt, IUpdatedAt
{
    public DateTime CreatedAt => Id.Time.UtcDateTime;

    public DateTime UpdatedAt { get; set; }
}