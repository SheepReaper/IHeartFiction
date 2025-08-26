using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Infrastructure;
using IHFiction.Data.Stories.Domain;

using MongoDB.Bson;
using MongoDB.EntityFrameworkCore.Storage.ValueConversion;
using IHFiction.Data.Searching.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

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

    // apply configurations from assembly except for WorkBodyConfiguration
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder
        .HasDefaultSchema(Schemas.Application)
        .ApplyConfigurationsFromAssembly(typeof(FictionDbContext).Assembly,
            config => !config.IsAssignableTo(typeof(IEntityTypeConfiguration<WorkBody>)))
        // .HasDbFunction(typeof(FictionDbContext).GetMethod(nameof(PromoteUserToAuthor), [typeof(string)])!)
        // .HasName("promote_user_to_author")
        // .HasSchema(Schemas.Application)
        // .HasParameter("userid", p => p.HasStoreType("character varying"))
        ;

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

    // public IQueryable<Author> PromoteUserToAuthor(string userid) => FromExpression(() => PromoteUserToAuthor(userid));
}
