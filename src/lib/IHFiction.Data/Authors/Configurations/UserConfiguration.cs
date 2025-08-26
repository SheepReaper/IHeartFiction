using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using IHFiction.Data.Authors.Domain;

namespace IHFiction.Data.Authors.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasQueryFilter(user => !user.DeletedAt.HasValue);

        builder.HasDiscriminator()
            .HasValue(nameof(User));

        builder.Property<string>("Discriminator")
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Save);
    }
}