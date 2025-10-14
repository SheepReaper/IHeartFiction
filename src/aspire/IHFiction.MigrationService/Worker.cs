using System.Diagnostics;

using Microsoft.EntityFrameworkCore;

using IHFiction.Data.Contexts;

namespace IHFiction.MigrationService;

internal sealed class Worker(IServiceProvider serviceProvider, IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    public const string ActivitySourceName = "IHFiction.MigrationService";
    private async Task ExecuteMigrationsAsync(ActivitySource activitySource, CancellationToken cancellationToken)
    {
        using var activity = activitySource.StartActivity("Migrating database", ActivityKind.Internal, null);

        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            await using var dbContext = scope.ServiceProvider.GetRequiredService<FictionDbContext>();

            await RunMigrationsAsync(dbContext, cancellationToken);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using ActivitySource activitySource = new(ActivitySourceName);

        await ExecuteMigrationsAsync(activitySource, stoppingToken);
        // await ExecuteSeedingAsync(activitySource, stoppingToken);

        hostApplicationLifetime.StopApplication();
    }

    private static async Task RunMigrationsAsync(FictionDbContext dbContext, CancellationToken stoppingToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () => await dbContext.Database.MigrateAsync(stoppingToken));
    }
}
