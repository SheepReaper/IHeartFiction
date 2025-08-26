using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Stories.Domain;
using IHFiction.Data.Stories.Configurations;

namespace IHFiction.Data.Contexts;

public class StoryDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<WorkBody> WorkBodies { get; init; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder
        .HasDefaultSchema(Schemas.Application)
        .ApplyConfiguration(new WorkBodyConfiguration());
}
