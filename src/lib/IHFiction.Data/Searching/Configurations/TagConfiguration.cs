using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using IHFiction.Data.Searching.Domain;

namespace IHFiction.Data.Searching.Configurations;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.Property(tag => tag.Category)
            .HasColumnName("category")
            .IsRequired();

        builder.Property(tag => tag.Subcategory)
            .HasColumnName("subcategory");

        builder.Property(tag => tag.Value)
            .HasColumnName("value")
            .IsRequired();

        builder.Property(tag => tag.NormalizedKey)
            .HasColumnName("normalized_key")
            .HasMaxLength(152)
            .IsRequired();

        builder.HasIndex(tag => tag.NormalizedKey)
            .IsUnique()
            .HasFilter("\"discriminator\" = 'CanonicalTag'");

        builder.HasMany(tag => tag.Works)
            .WithMany(work => work.Tags);
    }
}
