using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using IHFiction.Data.Notifications.Domain;

namespace IHFiction.Data.Notifications.Configurations;

internal sealed class UserAuthorFollowConfiguration : IEntityTypeConfiguration<UserAuthorFollow>
{
    public void Configure(EntityTypeBuilder<UserAuthorFollow> builder)
    {
        builder.ToTable("user_author_follows");

        builder.HasQueryFilter(follow =>
            follow.User.DeletedAt == null &&
            follow.Author.DeletedAt == null);

        builder.Property(follow => follow.UserId)
            .HasColumnName("user_id");

        builder.Property(follow => follow.AuthorId)
            .HasColumnName("author_id");

        builder.HasIndex(follow => new { follow.UserId, follow.AuthorId })
            .IsUnique();

        builder.HasOne(follow => follow.User)
            .WithMany()
            .HasForeignKey(follow => follow.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(follow => follow.Author)
            .WithMany()
            .HasForeignKey(follow => follow.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserStoryFollowConfiguration : IEntityTypeConfiguration<UserStoryFollow>
{
    public void Configure(EntityTypeBuilder<UserStoryFollow> builder)
    {
        builder.ToTable("user_story_follows");

        builder.HasQueryFilter(follow =>
            follow.User.DeletedAt == null &&
            follow.Story.DeletedAt == null);

        builder.Property(follow => follow.UserId)
            .HasColumnName("user_id");

        builder.Property(follow => follow.StoryId)
            .HasColumnName("story_id");

        builder.HasIndex(follow => new { follow.UserId, follow.StoryId })
            .IsUnique();

        builder.HasOne(follow => follow.User)
            .WithMany()
            .HasForeignKey(follow => follow.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(follow => follow.Story)
            .WithMany()
            .HasForeignKey(follow => follow.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DeviceAuthorFollowConfiguration : IEntityTypeConfiguration<DeviceAuthorFollow>
{
    public void Configure(EntityTypeBuilder<DeviceAuthorFollow> builder)
    {
        builder.ToTable("device_author_follows");

        builder.HasQueryFilter(follow =>
            follow.Author.DeletedAt == null);

        builder.Property(follow => follow.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(100);

        builder.Property(follow => follow.AuthorId)
            .HasColumnName("author_id");

        builder.HasIndex(follow => new { follow.DeviceId, follow.AuthorId })
            .IsUnique();

        builder.HasOne(follow => follow.Author)
            .WithMany()
            .HasForeignKey(follow => follow.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DeviceStoryFollowConfiguration : IEntityTypeConfiguration<DeviceStoryFollow>
{
    public void Configure(EntityTypeBuilder<DeviceStoryFollow> builder)
    {
        builder.ToTable("device_story_follows");

        builder.HasQueryFilter(follow =>
            follow.Story.DeletedAt == null);

        builder.Property(follow => follow.DeviceId)
            .HasColumnName("device_id")
            .HasMaxLength(100);

        builder.Property(follow => follow.StoryId)
            .HasColumnName("story_id");

        builder.HasIndex(follow => new { follow.DeviceId, follow.StoryId })
            .IsUnique();

        builder.HasOne(follow => follow.Story)
            .WithMany()
            .HasForeignKey(follow => follow.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}