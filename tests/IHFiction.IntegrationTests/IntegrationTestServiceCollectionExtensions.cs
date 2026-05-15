using IHFiction.Data;
using IHFiction.Data.Contexts;
using IHFiction.Data.Stories.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using MongoDB.Driver;

using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace IHFiction.IntegrationTests;

internal static class IntegrationTestServiceCollectionExtensions
{
    public static IServiceCollection AddKeyedTestFictionDbContext<TTest>(
        this IServiceCollection services,
        Action<NpgsqlDbContextOptionsBuilder>? configureNpgsql = null,
        bool configurePendingModelWarning = true,
        bool useDefaultInterceptors = true)
    {
        services.AddKeyedScoped(
            typeof(TTest).Name,
            (sp, key) =>
            {
                var connectionString = sp.GetRequiredService<PgsqlConnectionStringProvider>()
                    .GetConnectionStringForDatabase($"test_{typeof(TTest).Name}");

                var builder = new DbContextOptionsBuilder<FictionDbContext>()
                    .UseNpgsql(connectionString, options => configureNpgsql?.Invoke(options))
                    .UseSnakeCaseNamingConvention();

                if (configurePendingModelWarning)
                {
                    builder.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));
                }

                if (useDefaultInterceptors)
                {
                    builder.WithDefaultInterceptors(sp.GetRequiredService<TimeProvider>());
                }

                return new FictionDbContext(builder.Options);
            });

        return services;
    }

    public static IServiceCollection AddKeyedTestWorkBodyCollection<TTest>(this IServiceCollection services)
    {
        services.AddKeyedScoped(
            typeof(TTest).Name,
            (sp, key) => sp.GetRequiredService<IMongoClient>()
                .GetDatabase($"test_stories_{typeof(TTest).Name}")
                .GetCollection<WorkBody>("works"));

        return services;
    }
}