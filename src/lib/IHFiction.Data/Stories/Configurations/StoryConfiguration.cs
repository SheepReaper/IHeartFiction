using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IHFiction.Data.Stories.Domain;

namespace IHFiction.Data.Stories.Configurations;

internal sealed class StoryConfiguration : IEntityTypeConfiguration<Story>
{
    public void Configure(EntityTypeBuilder<Story> builder)
    {
        builder.Property(story => story.Description)
            .HasColumnName("description")
            .IsRequired();

        builder.Property(story => story.WorkBodyId)
            .HasColumnName("work_body_id");

        builder.HasMany(story => story.Chapters)
            .WithOne(chapter => chapter.Story)
            .HasForeignKey(c => c.StoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(story => story.Books)
            .WithOne(book => book.Story)
            .HasForeignKey(b => b.StoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
