using IHFiction.Data.Authors.Domain;
using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Notifications.Domain;

public sealed class UserPushSubscription : DomainUlidEntityWithTimestamp
{
    public Ulid UserId { get; set; }
    public User User { get; set; } = default!;
    public string Endpoint { get; set; } = default!;
    public string P256dhKey { get; set; } = default!;
    public string AuthKey { get; set; } = default!;
    public DateTime? ExpiresAt { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? LastSuccessfulDeliveryAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
}

public sealed class DevicePushSubscription : DomainUlidEntityWithTimestamp
{
    public string DeviceId { get; set; } = default!;
    public string Endpoint { get; set; } = default!;
    public string P256dhKey { get; set; } = default!;
    public string AuthKey { get; set; } = default!;
    public DateTime? ExpiresAt { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? LastSuccessfulDeliveryAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
}