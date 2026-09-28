using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

using IHFiction.Data;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;

using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

namespace IHFiction.FictionApi.Extensions;

internal static class PersistenceExtensions
{
    public static IHostApplicationBuilder AddPersistenceServices(
        this IHostApplicationBuilder builder,
        TimeProvider timeProvider,
        bool isBuildEnvironment)
    {
        if (!isBuildEnvironment && builder.Environment.IsProduction())
        {
            builder.Services.AddDataProtection()
                .PersistKeysToDbContext<FictionDbContext>()
                .SetApplicationName(builder.Environment.ApplicationName);
        }

        // Configure database connections
        builder.AddNpgsqlDbContext<FictionDbContext>(
            "fiction-db",
            configureDbContextOptions: (options) => options
                .UseNpgsql(options => options
                    .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application)
                    .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                .UseSnakeCaseNamingConvention()
                .WithDefaultInterceptors(timeProvider));

        builder.AddMongoDBClient("stories-db",
            null,
            settings => settings.ClusterConfigurator = c => c.Subscribe(
                new DiagnosticsActivityEventSubscriber(
                    new InstrumentationOptions
                    {
                        CaptureCommandText = true
                    }
                ))
        );

        builder.AddRedisClient("redis");

        builder.Services.AddSingleton(services => services
            .GetRequiredService<IMongoDatabase>()
            .GetCollection<WorkBody>("works"));

        return builder;
    }
}
