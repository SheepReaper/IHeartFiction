namespace IHFiction.SharedKernel.Entities;

public abstract class DomainEntity<TKey> : IHasId<TKey> where TKey : struct
{
    public TKey Id { get; set; }
}
