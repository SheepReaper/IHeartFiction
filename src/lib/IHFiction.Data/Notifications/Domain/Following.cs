using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Stories.Domain;
using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Notifications.Domain;

public sealed class UserAuthorFollow : DomainUlidEntityWithTimestamp
{
    public Ulid UserId { get; set; }
    public User User { get; set; } = default!;
    public Ulid AuthorId { get; set; }
    public Author Author { get; set; } = default!;
}

public sealed class UserStoryFollow : DomainUlidEntityWithTimestamp
{
    public Ulid UserId { get; set; }
    public User User { get; set; } = default!;
    public Ulid StoryId { get; set; }
    public Story Story { get; set; } = default!;
}

public sealed class DeviceAuthorFollow : DomainUlidEntityWithTimestamp
{
    public string DeviceId { get; set; } = default!;
    public Ulid AuthorId { get; set; }
    public Author Author { get; set; } = default!;
}

public sealed class DeviceStoryFollow : DomainUlidEntityWithTimestamp
{
    public string DeviceId { get; set; } = default!;
    public Ulid StoryId { get; set; }
    public Story Story { get; set; } = default!;
}