using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Infrastructure;
using IHFiction.Data.Searching.Domain;
using IHFiction.Data.Stories.Domain;

using MongoDB.Bson;
using MongoDB.EntityFrameworkCore.Storage.ValueConversion;

namespace IHFiction.Data.Contexts;

public class FictionDbContext(DbContextOptions options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Author> Authors { get; set; } = null!;
    public DbSet<Story> Stories { get; set; } = null!;
    public DbSet<Book> Books { get; set; } = null!;
    public DbSet<Chapter> Chapters { get; set; } = null!;
    public DbSet<Tag> Tags { get; set; } = null!;
    public DbSet<Anthology> Anthologies { get; set; } = null!;
    public DbSet<Work> Works { get; set; } = null!;

    // apply configurations from assembly except for WorkBodyConfiguration
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder
        .HasDefaultSchema(Schemas.Application)
        .ApplyConfigurationsFromAssembly(typeof(FictionDbContext).Assembly,
            config => !config.IsAssignableTo(typeof(IEntityTypeConfiguration<WorkBody>)));

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder
            .Properties<Ulid>()
            .HaveConversion<UlidToStringConverter>();

        configurationBuilder
            .Properties<ObjectId>()
            .HaveConversion<ObjectIdToStringConverter>();
    }
}
