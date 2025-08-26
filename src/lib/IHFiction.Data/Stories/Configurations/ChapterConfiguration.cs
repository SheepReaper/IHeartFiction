namespace IHFiction.Data.Stories.Configurations;

using IHFiction.Data.Stories.Domain;


using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
    public void Configure(EntityTypeBuilder<Chapter> builder)
    {
        builder.Property(chapter => chapter.StoryId)
            .HasColumnName("story_id");

        builder.Property(chapter => chapter.BookId)
            .HasColumnName("book_id");

        builder.Property(chapter => chapter.WorkBodyId)
            .HasColumnName("work_body_id")
            .IsRequired();
    }
}
