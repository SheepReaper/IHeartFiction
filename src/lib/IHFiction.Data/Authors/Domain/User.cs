using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Authors.Domain;

public class User : DomainUlidEntityWithTimestamp, ISoftDeletable
{
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public string? GravatarEmail { get; set; }

    public DateTime? DeletedAt { get; set; }

    public static User FromUserId(Guid userId, string? displayName = null) => new()
    {
        UserId = userId,
        Name = displayName ?? $"Unknown User {userId}"
    };
}
