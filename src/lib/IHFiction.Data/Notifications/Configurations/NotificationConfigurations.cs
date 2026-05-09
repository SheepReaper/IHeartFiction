using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using IHFiction.Data.Notifications.Domain;

namespace IHFiction.Data.Notifications.Configurations;

internal sealed class NotificationRecordConfiguration : IEntityTypeConfiguration<NotificationRecord>
{
    public void Configure(EntityTypeBuilder<NotificationRecord> builder)
    {
        builder.ToTable("notifications");

        builder.Property(notification => notification.NotificationKey)
            .HasColumnName("notification_key")
            .HasMaxLength(200);

        builder.Property(notification => notification.Kind)
            .HasColumnName("kind")
            .HasMaxLength(50);

        builder.Property(notification => notification.Title)
            .HasColumnName("title")
            .HasMaxLength(200);

        builder.Property(notification => notification.Body)
            .HasColumnName("body")
            .HasMaxLength(500);

        builder.Property(notification => notification.TargetPath)
            .HasColumnName("target_path")
            .HasMaxLength(500);

        builder.Property(notification => notification.EventOccurredAt)
            .HasColumnName("event_occurred_at");

        builder.Property(notification => notification.AuthorId)
            .HasColumnName("author_id");

        builder.Property(notification => notification.StoryId)
            .HasColumnName("story_id");

        builder.Property(notification => notification.ChapterId)
            .HasColumnName("chapter_id");

        builder.HasIndex(notification => notification.NotificationKey)
            .IsUnique();

        builder.HasIndex(notification => notification.AuthorId);
        builder.HasIndex(notification => notification.StoryId);
        builder.HasIndex(notification => notification.ChapterId);

        builder.HasOne(notification => notification.Author)
            .WithMany()
            .HasForeignKey(notification => notification.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(notification => notification.Story)
            .WithMany()
            .HasForeignKey(notification => notification.StoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(notification => notification.Chapter)
            .WithMany()
            .HasForeignKey(notification => notification.ChapterId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class UserNotificationDeliveryConfiguration : IEntityTypeConfiguration<UserNotificationDelivery>
{
    public void Configure(EntityTypeBuilder<UserNotificationDelivery> builder)
    {
        builder.ToTable("user_notification_deliveries");

        builder.Property(delivery => delivery.NotificationId)
            .HasColumnName("notification_id");

        builder.Property(delivery => delivery.UserId)
            .HasColumnName("user_id");

        builder.Property(delivery => delivery.DeliveredAt)
            .HasColumnName("delivered_at");

        builder.Property(delivery => delivery.ReadAt)
            .HasColumnName("read_at");

        builder.Ignore(delivery => delivery.IsRead);

        builder.HasIndex(delivery => new { delivery.UserId, delivery.NotificationId })
            .IsUnique();

        builder.HasOne(delivery => delivery.Notification)
            .WithMany()
            .HasForeignKey(delivery => delivery.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(delivery => delivery.User)
            .WithMany()
            .HasForeignKey(delivery => delivery.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DeviceNotificationDeliveryConfiguration : IEntityTypeConfiguration<DeviceNotificationDelivery>
{
    public void Configure(EntityTypeBuilder<DeviceNotificationDelivery> builder)
    {
        builder.ToTable("device_notification_deliveries");

        builder.Property(delivery => delivery.NotificationId)
            .HasColumnName("notification_id");

        builder.Property(delivery => delivery.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(100);

        builder.Property(delivery => delivery.DeliveredAt)
            .HasColumnName("delivered_at");

        builder.Property(delivery => delivery.ReadAt)
            .HasColumnName("read_at");

        builder.Ignore(delivery => delivery.IsRead);

        builder.HasIndex(delivery => new { delivery.DeviceId, delivery.NotificationId })
            .IsUnique();

        builder.HasOne(delivery => delivery.Notification)
            .WithMany()
            .HasForeignKey(delivery => delivery.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}