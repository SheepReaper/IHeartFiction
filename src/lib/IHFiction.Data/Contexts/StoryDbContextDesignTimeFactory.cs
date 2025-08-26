using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IHFiction.Data.Contexts;

internal sealed class StoryDbContextDesignTimeFactory : IDesignTimeDbContextFactory<StoryDbContext>
{
    public StoryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StoryDbContext>();

        optionsBuilder
            .UseMongoDB("stories-db", "stories-db")
            .UseSnakeCaseNamingConvention();

        return new StoryDbContext(optionsBuilder.Options);
    }
}