using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using IHFiction.Data.Stories.Domain;

namespace IHFiction.Data.Stories.Configurations;

internal sealed class WorkConfiguration : IEntityTypeConfiguration<Work>
{
    public void Configure(EntityTypeBuilder<Work> builder)
    {
        // Configure Table Per Hierarchy (TPH) inheritance
        // builder.HasDiscriminator<string>("discriminator")
        //     .HasValue<Story>("Story")
        //     .HasValue<Book>("Book")
        //     .HasValue<Anthology>("Anthology")
        //     .HasValue<Chapter>("Chapter");

        builder.HasQueryFilter(work => !work.DeletedAt.HasValue);

        builder.HasMany(work => work.Authors)
            .WithMany(author => author.Works);

        builder.HasOne(work => work.Owner)
            .WithMany(author => author.OwnedWorks)
            .HasForeignKey(work => work.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(work => work.Title);
    }
}
