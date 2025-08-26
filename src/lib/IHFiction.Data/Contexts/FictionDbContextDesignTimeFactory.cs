using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IHFiction.Data.Contexts;

internal sealed class FictionDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FictionDbContext>
{
    public FictionDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FictionDbContext>();

        optionsBuilder
            .UseNpgsql(options => options.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
            .UseSnakeCaseNamingConvention();

        return new FictionDbContext(optionsBuilder.Options);
    }
}
