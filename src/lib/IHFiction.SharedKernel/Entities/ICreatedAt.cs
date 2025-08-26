namespace IHFiction.SharedKernel.Entities;

public interface ICreatedAt : IReadCreatedAt
{
    new DateTime CreatedAt { get; set; }
}
