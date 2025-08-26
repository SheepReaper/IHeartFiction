using IHFiction.Data.Stories.Domain;

namespace IHFiction.Data.Authors.Domain;

public sealed class Author : User
{
    public Profile Profile { get; set; } = new();

    private ICollection<Work>? _works;
    public ICollection<Work> Works => _works ??= [];

    private ICollection<Work>? _ownedWorks;
    public ICollection<Work> OwnedWorks => _ownedWorks ??= [];

    public static Author FromUser(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new()
        {
            Id = user.Id,
            UserId = user.UserId,
            Name = user.Name,
            GravatarEmail = user.GravatarEmail,
            UpdatedAt = user.UpdatedAt,
            DeletedAt = user.DeletedAt
        };
    }
}