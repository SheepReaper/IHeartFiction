namespace IHFiction.SharedKernel.Entities;

public interface IDerivedCreatedAt : IHasId<Ulid>, IReadCreatedAt
{
    new DateTime CreatedAt => Id.Time.UtcDateTime;
}
