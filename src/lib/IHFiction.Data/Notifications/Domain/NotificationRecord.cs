using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Stories.Domain;
using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Notifications.Domain;

public static class NotificationKinds
{
    public const string StoryPublished = "story_published";
    public const string ChapterPublished = "chapter_published";
}

public sealed class NotificationRecord : DomainUlidEntityWithTimestamp
{
    public string NotificationKey { get; set; } = default!;
    public string Kind { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string TargetPath { get; set; } = default!;
    public DateTime EventOccurredAt { get; set; }

    public Ulid AuthorId { get; set; }
    public Author Author { get; set; } = default!;
    public Ulid? StoryId { get; set; }
    public Story? Story { get; set; }
    public Ulid? ChapterId { get; set; }
    public Chapter? Chapter { get; set; }
}

public sealed class UserNotificationDelivery : DomainUlidEntityWithTimestamp
{
    public Ulid NotificationId { get; set; }
    public NotificationRecord Notification { get; set; } = default!;
    public Ulid UserId { get; set; }
    public User User { get; set; } = default!;
    public DateTime DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsRead => ReadAt.HasValue;
}

public sealed class DeviceNotificationDelivery : DomainUlidEntityWithTimestamp
{
    public Ulid NotificationId { get; set; }
    public NotificationRecord Notification { get; set; } = default!;
    public string DeviceId { get; set; } = default!;
    public DateTime DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsRead => ReadAt.HasValue;
}