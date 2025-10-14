using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

using IHFiction.Data;
using IHFiction.Data.Contexts;
using IHFiction.MigrationService;

using OpenTelemetry.Exporter;

var builder = Host.CreateApplicationBuilder(args);

if (builder.Configuration["SecretsPath"] is string secretsPath)
    builder.Configuration.AddKeyPerFile(secretsPath, optional: true, reloadOnChange: true);

if (builder.Configuration["Dashboard:Otlp:PrimaryApiKey"] is string otlpApiKey)
    builder.Services.Configure<OtlpExporterOptions>(o => o.Headers = $"x-otlp-api-key={otlpApiKey}");
    
else if(builder.Environment.IsProduction()) throw new InvalidOperationException("Dashboard:Otlp:PrimaryApiKey configuration is required");

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

var host = builder.Build();

await host.RunAsync();
