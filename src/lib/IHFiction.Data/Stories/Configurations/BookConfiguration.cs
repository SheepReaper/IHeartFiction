namespace IHFiction.Data.Stories.Configurations;

using IHFiction.Data.Stories.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.Property(book => book.Description)
            .HasColumnName("description")
            .IsRequired();

        builder.Property(book => book.StoryId)
            .HasColumnName("story_id");

        builder.HasMany(book => book.Chapters)
            .WithOne(chapter => chapter.Book)
            .HasForeignKey(c => c.BookId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
