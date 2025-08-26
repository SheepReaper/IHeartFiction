namespace IHFiction.SharedKernel.Entities;

public abstract class DomainUlidEntity : DomainEntity<Ulid>
{
    public new Ulid Id { get; set; } = Ulid.NewUlid();
}