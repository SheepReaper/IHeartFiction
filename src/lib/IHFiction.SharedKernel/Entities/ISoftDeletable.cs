namespace IHFiction.SharedKernel.Entities;

public interface ISoftDeletable
{
    DateTime? DeletedAt { get; set; }

    public bool IsDeleted => DeletedAt.HasValue;
}
