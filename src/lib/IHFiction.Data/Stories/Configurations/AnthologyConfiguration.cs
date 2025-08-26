namespace IHFiction.Data.Stories.Configurations;

using IHFiction.Data.Stories.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class AnthologyConfiguration : IEntityTypeConfiguration<Anthology>
{
    public void Configure(EntityTypeBuilder<Anthology> builder)
    {
        builder.Property(anthology => anthology.Description)
            .HasColumnName("description")
            .IsRequired();

        builder.HasMany(anthology => anthology.Stories)
            .WithMany(story => story.Anthologies);
    }
}