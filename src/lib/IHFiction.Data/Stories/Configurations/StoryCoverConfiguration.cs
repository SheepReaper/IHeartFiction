using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using IHFiction.Data.Stories.Domain;

namespace IHFiction.Data.Stories.Configurations;

internal sealed class StoryCoverConfiguration : IEntityTypeConfiguration<StoryCover>
{
    public void Configure(EntityTypeBuilder<StoryCover> builder)
    {
        builder.Property(cover => cover.StoryId)
            .HasColumnName("story_id")
            .IsRequired();

        builder.Property(cover => cover.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(cover => cover.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(cover => cover.FileSizeBytes)
            .HasColumnName("file_size_bytes")
            .IsRequired();

        builder.Property(cover => cover.ContentHash)
            .HasColumnName("content_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(cover => cover.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.HasIndex(cover => cover.StoryId)
            .IsUnique();
    }
}