using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using IHFiction.Data.Notifications.Domain;

namespace IHFiction.Data.Notifications.Configurations;

internal sealed class UserPushSubscriptionConfiguration : IEntityTypeConfiguration<UserPushSubscription>
{
    public void Configure(EntityTypeBuilder<UserPushSubscription> builder)
    {
        builder.ToTable("user_push_subscriptions");

        builder.Property(subscription => subscription.UserId)
            .HasColumnName("user_id");

        builder.Property(subscription => subscription.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(500);

        builder.Property(subscription => subscription.P256dhKey)
            .HasColumnName("p256dh_key")
            .HasMaxLength(200);

        builder.Property(subscription => subscription.AuthKey)
            .HasColumnName("auth_key")
            .HasMaxLength(200);

        builder.Property(subscription => subscription.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(subscription => subscription.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(subscription => subscription.LastSuccessfulDeliveryAt)
            .HasColumnName("last_successful_delivery_at");

        builder.Property(subscription => subscription.LastFailureAt)
            .HasColumnName("last_failure_at");

        builder.HasIndex(subscription => subscription.Endpoint)
            .IsUnique();

        builder.HasIndex(subscription => subscription.UserId);

        builder.HasOne(subscription => subscription.User)
            .WithMany()
            .HasForeignKey(subscription => subscription.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DevicePushSubscriptionConfiguration : IEntityTypeConfiguration<DevicePushSubscription>
{
    public void Configure(EntityTypeBuilder<DevicePushSubscription> builder)
    {
        builder.ToTable("device_push_subscriptions");

        builder.Property(subscription => subscription.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(100);

        builder.Property(subscription => subscription.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(500);

        builder.Property(subscription => subscription.P256dhKey)
            .HasColumnName("p256dh_key")
            .HasMaxLength(200);

        builder.Property(subscription => subscription.AuthKey)
            .HasColumnName("auth_key")
            .HasMaxLength(200);

        builder.Property(subscription => subscription.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(subscription => subscription.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        builder.Property(subscription => subscription.LastSuccessfulDeliveryAt)
            .HasColumnName("last_successful_delivery_at");

        builder.Property(subscription => subscription.LastFailureAt)
            .HasColumnName("last_failure_at");

        builder.HasIndex(subscription => subscription.Endpoint)
            .IsUnique();

        builder.HasIndex(subscription => subscription.DeviceId);
    }
}