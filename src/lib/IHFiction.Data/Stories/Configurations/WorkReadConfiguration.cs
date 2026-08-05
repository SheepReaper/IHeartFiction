using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using IHFiction.Data.Stories.Domain;

namespace IHFiction.Data.Stories.Configurations;

internal sealed class WorkReadConfiguration : IEntityTypeConfiguration<WorkRead>
{
    public void Configure(EntityTypeBuilder<WorkRead> builder)
    {
        builder.ToTable("work_reads");
        builder.HasQueryFilter(read => !read.Work.DeletedAt.HasValue);
        builder.Property(read => read.ReaderKey).HasMaxLength(66).IsRequired();
        builder.Property(read => read.FirstReadAt).IsRequired();
        builder.Property(read => read.LastReadAt).IsRequired();
        builder.HasIndex(read => new { read.WorkId, read.ReaderKey }).IsUnique();
        builder.HasIndex(read => new { read.ReaderKey, read.LastReadAt });
        builder.HasOne(read => read.Work)
            .WithMany()
            .HasForeignKey(read => read.WorkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
