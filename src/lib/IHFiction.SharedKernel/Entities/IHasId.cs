namespace IHFiction.SharedKernel.Entities;

public interface IHasId<TId> where TId : struct
{
    TId Id { get; set; }
}
