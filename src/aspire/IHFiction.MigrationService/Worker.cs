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

    // private async Task ExecuteSeedingAsync(ActivitySource activitySource, CancellationToken cancellationToken)
    // {
    //     using var activity = activitySource.StartActivity("Applying seed data", ActivityKind.Internal, null);

    //     try
    //     {
    //         await using var scope = serviceProvider.CreateAsyncScope();
    //         await using var dbContext = scope.ServiceProvider.GetRequiredService<FictionDbContext>();
    //         await using var storyDbContext = scope.ServiceProvider.GetRequiredService<StoryDbContext>();

    //         await SeedTestDataAsync(dbContext, storyDbContext, cancellationToken);
    //     }
    //     catch (Exception ex)
    //     {
    //         activity?.AddException(ex);
    //         throw;
    //     }
    // }

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

    // private static async Task SeedTestDataAsync(FictionDbContext fdbContext, StoryDbContext sdbContext, CancellationToken cancellationToken)
    // {
    //     var modifying = false;

    //     const string note1 = "TEST BODY 1";
    //     const string authorName = "Test Author";
    //     const string userName = "Test User";

    //     var workBody = await sdbContext.WorkBodies.FirstOrDefaultAsync(w => w.Note1 == note1, cancellationToken);
    //     var author = await fdbContext.Authors.FirstOrDefaultAsync(a => a.Name == authorName, cancellationToken);
    //     var user = await fdbContext.Users.FirstOrDefaultAsync(u => u.Name == userName, cancellationToken);
    //     var dt = fdbContext.GetService<TimeProvider>();

    //     if (workBody is not null && author is not null && user is not null) return;

    //     var strategy = fdbContext.Database.CreateExecutionStrategy();

    //     await strategy.ExecuteAsync(async () =>
    //     {
    //         using TransactionScope transaction = new(TransactionScopeAsyncFlowOption.Enabled);

    //         if (workBody is null)
    //         {
    //             workBody = new()
    //             {
    //                 Note1 = note1,
    //                 Note2 = "TEST BODY 2",
    //                 Content = "This is a test body.",
    //                 UpdatedAt = dt.GetUtcNow().UtcDateTime
    //             };

    //             sdbContext.WorkBodies.Add(workBody);

    //             await sdbContext.SaveChangesAsync(cancellationToken);

    //             modifying = true;
    //         }

    //         if (author is null)
    //         {
    //             author = new()
    //             {
    //                 Name = authorName,
    //             };

    //             fdbContext.Authors.Add(author);

    //             modifying = true;
    //         }

    //         if (modifying)
    //         {
    //             author.Works.Add(new Story()
    //             {
    //                 Title = "Test Story",
    //                 Description = "This is a test story.",
    //                 WorkBodyId = workBody.Id,
    //                 Owner = author
    //             });
    //         }

    //         if (user is null)
    //         {
    //             user = new()
    //             {
    //                 Name = userName
    //             };

    //             fdbContext.Users.Add(user);

    //             modifying = true;
    //         }

    //         if (modifying)
    //         {
    //             await fdbContext.SaveChangesAsync(cancellationToken);
    //         }

    //         transaction.Complete();
    //     });
    // }
}
