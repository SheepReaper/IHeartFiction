using IHFiction.Data.Searching.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IHFiction.Data.Searching.Configurations;

internal sealed class CanonicalTagConfiguration : IEntityTypeConfiguration<CanonicalTag>
{
    public void Configure(EntityTypeBuilder<CanonicalTag> builder)
    {
        builder.HasMany(tag => tag.Synonyms)
            .WithOne(synonym => synonym.CanonicalTag)
            .HasForeignKey(tag => tag.CanonicalTagId);
    }
}
