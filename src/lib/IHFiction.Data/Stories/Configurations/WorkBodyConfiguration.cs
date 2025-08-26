using IHFiction.Data.Stories.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using MongoDB.EntityFrameworkCore.Extensions;

namespace IHFiction.Data.Stories.Configurations;

internal sealed class WorkBodyConfiguration : IEntityTypeConfiguration<WorkBody>
{
    public void Configure(EntityTypeBuilder<WorkBody> builder)
    {
        builder.ToCollection("works").HasKey(wb => wb.Id);
    }
}
