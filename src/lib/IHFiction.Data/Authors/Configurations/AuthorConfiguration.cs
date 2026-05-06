using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IHFiction.Data.Authors.Domain;

namespace IHFiction.Data.Authors.Configurations;

internal sealed class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.OwnsOne(author => author.Profile, profileBuilder =>
        {
            profileBuilder.Property(profile => profile.Bio)
                .HasColumnName("profile_bio");

            profileBuilder.OwnsMany(profile => profile.SocialLinks, socialLinkBuilder =>
            {
                socialLinkBuilder.ToTable("author_social_links");

                socialLinkBuilder.WithOwner().HasForeignKey("author_id");

                socialLinkBuilder.Property<Ulid>("author_id");

                socialLinkBuilder.Property(link => link.Type)
                    .HasColumnName("type")
                    .HasMaxLength(50)
                    .IsRequired();

                socialLinkBuilder.Property(link => link.Value)
                    .HasColumnName("value")
                    .HasMaxLength(500)
                    .IsRequired();

                socialLinkBuilder.HasKey("author_id", nameof(SocialLink.Type));

                socialLinkBuilder.HasIndex("author_id", nameof(SocialLink.Type))
                    .IsUnique();
            });
        });
    }
}
