using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

using Cysharp.Serialization.Json;

using IHFiction.Data;
using IHFiction.Data.Contexts;
using IHFiction.Data.Infrastructure;
using IHFiction.FictionApi.Authors;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;

using Keycloak.AuthServices.Authorization;

using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

using Scalar.AspNetCore;
using IHFiction.SharedKernel.Markdown;
using IHFiction.SharedKernel.Linking;

var builder = WebApplication.CreateBuilder(args);

// Initialize shared services
TimeProvider dateTime = TimeProvider.System;

// Add Aspire service defaults (must be first)
builder.AddServiceDefaults();

// Configure core ASP.NET Core services
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions.Add("requestId", context.HttpContext.TraceIdentifier));

builder.Services.AddValidation(); // .NET 10 built-in validation support for minimal APIs
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.Configure<RouteOptions>(options => options.ConstraintMap.Add("ulid", typeof(UlidRouteConstraint)));

builder.Services.AddHttpContextAccessor();

// Configure JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = builder.Environment.IsDevelopment();

    options.SerializerOptions.Converters.Add(new UlidJsonConverter());
    options.SerializerOptions.Converters.Add(new ObjectIdJsonConverter());
    options.SerializerOptions.Converters.Add(new LinkedConverterFactory());
});

if (Assembly.GetEntryAssembly()?.GetName().Name != "GetDocument.Insider")
{
    builder.Services.AddDataProtection()
        .PersistKeysToDbContext<FictionDbContext>()
        .SetApplicationName(builder.Environment.ApplicationName);
}

// Configure CORS
builder.Services.AddCors(o =>
{
    if (builder.Environment.IsDevelopment())
    {
        o.AddDefaultPolicy(p => p
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
    }
    else
    {
        var allowedOrigins = builder.Configuration
            .GetValue<string>("Cors:AllowedOrigins")
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? throw new InvalidOperationException("Cors:AllowedOrigins configuration is required in production");

        o.AddDefaultPolicy(p => p
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
    }
});

// Configure shared infrastructure services
builder.Services.AddSingleton(dateTime);
builder.Services.Configure<MarkdownOptions>(builder.Configuration.GetSection(MarkdownOptions.SectionName));

// Configure database connections
builder.AddNpgsqlDbContext<FictionDbContext>(
    "fiction-db",
    configureDbContextOptions: (options) => options
        .UseNpgsql(options => options.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
        .UseSnakeCaseNamingConvention()
        .WithDefaultInterceptors(dateTime));

builder.AddMongoDBClient("stories-db",
    null,
    // options => options.DisableHealthChecks = true,
    settings => settings.ClusterConfigurator = c => c.Subscribe(
        new DiagnosticsActivityEventSubscriber(
            new InstrumentationOptions()
            {
                CaptureCommandText = true
            }
        ))
);

builder.Services.AddDbContextFactory<StoryDbContext>((services, options) => options
    .UseMongoDB(services.GetRequiredService<IMongoClient>(), "stories-db")
    .UseSnakeCaseNamingConvention());

builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer("keycloak", realm: "fiction", options =>
    {
        options.Audience = "fiction-api";

        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false;
        }

        // Allow for a small clock drift between the API and the identity provider
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("author", p => p.RequireRole("author"));

builder.Services.AddKeycloakAuthorization(options =>
{
    options.RoleClaimType = ClaimTypes.Role;
    options.EnableRolesMapping = RolesClaimTransformationSource.All;
    options.Resource = "fiction-api";
});

#pragma warning disable S1075 // URIs should not be hardcoded
builder.Services.AddKeycloakRealmAdminClient<KeycloakAdminService>(
    "https+http://keycloak",
    "fiction-admin-client",
    "fiction");
#pragma warning restore S1075 // URIs should not be hardcoded

// Configure OpenAPI documentation
#pragma warning disable S1075 // URIs should not be hardcoded
builder.Services.AddOpenApiWithAuth(builder.Configuration["services:keycloak:http:0"] ?? "https://localhost:8080/", "fiction");
#pragma warning restore S1075 // URIs should not be hardcoded

// Register application services
// Core services
builder.Services.AddSingleton<KeycloakAdminService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AuthorizationService>();
builder.Services.AddScoped<EntityLoaderService>();

builder.Services.AddTransient<LinkService>();

// Configure pagination options
builder.Services.AddPagination();

// Automatically register all use case classes
builder.Services.AddUseCases();

// Register endpoints
builder.Services.AddEndpoints();

var app = builder.Build();

// Configure middleware pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseCors();

// Configure authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Configure development-only features
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o
        .AddPreferredSecuritySchemes("OAuth2")
        .AddAuthorizationCodeFlow("OAuth2", flow => flow
            .WithClientId("fiction-api-docs")
            .WithSelectedScopes("fiction_api"))
        .WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl));
}

// Map endpoints
app.MapEndpoints();
app.MapDefaultEndpoints();

await app.RunAsync();
