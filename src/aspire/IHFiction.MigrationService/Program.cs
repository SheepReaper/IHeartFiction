using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

using IHFiction.Data;
using IHFiction.Data.Contexts;
using IHFiction.MigrationService;

using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

var builder = Host.CreateApplicationBuilder(args);

if (builder.Configuration["SecretsPath"] is string secretsPath)
    builder.Configuration.AddKeyPerFile(secretsPath, optional: true, reloadOnChange: true);

TimeProvider dateTime = TimeProvider.System;

builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton(dateTime);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

builder.AddNpgsqlDbContext<FictionDbContext>(
    "fiction-db",
    configureDbContextOptions: (options) => options
        .UseNpgsql(options => options.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
        .UseSnakeCaseNamingConvention()
        .WithDefaultInterceptors(dateTime));

builder.AddMongoDBClient("stories-db", null, settings => settings
    .ClusterConfigurator = c => c.Subscribe(
        new DiagnosticsActivityEventSubscriber(
            new InstrumentationOptions()
            {
                CaptureCommandText = true
            }
        )
    )
);

builder.Services.AddDbContextFactory<StoryDbContext>((services, options) => options
    .UseMongoDB(services.GetRequiredService<IMongoClient>(), "stories-db")
    .UseSnakeCaseNamingConvention());

var host = builder.Build();

await host.RunAsync();
