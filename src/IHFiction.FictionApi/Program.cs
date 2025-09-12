using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using Cysharp.Serialization.Json;

using IHFiction.Data;
using IHFiction.Data.Contexts;
using IHFiction.Data.Infrastructure;
using IHFiction.FictionApi.Authors;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Markdown;

using Keycloak.AuthServices.Authorization;

using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Configuration["SecretsPath"] is string secretsPath)
    builder.Configuration.AddKeyPerFile(secretsPath, optional: true, reloadOnChange: true);


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
if (builder.Environment.IsProduction())
{
    string[] allowedOrigins = [.. (builder.Configuration["AllowedOrigins"]
        ?? throw new InvalidOperationException("AllowedOrigins configuration is required in production"))
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()));
}

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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddKeycloakJwtBearer("keycloak", realm: "fiction", JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Audience = "fiction-api";

        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false;
        }
        else
        {
            if (builder.Configuration["OidcAuthority"] is string authority)
                options.Authority = authority;
        }

        // Allow for a small clock drift between the API and the identity provider
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(2);
    })
    // This is here to make configuring the docs client easier
    .AddKeycloakOpenIdConnect("keycloak", "fiction", OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = "fiction-api-docs";
        options.Scope.Add("fiction_api");
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.Resource = "fiction-api";

        options.TokenValidationParameters.NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.PreferredUsername;

        if (builder.Environment.IsDevelopment() || Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
        {
            options.RequireHttpsMetadata = false;
        }
        else
        {
            if (builder.Configuration["OidcAuthority"] is string authority)
                options.Authority = authority;
        }
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("author", p => p.RequireRole("author"));

builder.Services.AddKeycloakAuthorization(options =>
{
    options.RoleClaimType = ClaimTypes.Role;
    options.EnableRolesMapping = RolesClaimTransformationSource.All;
    options.Resource = "fiction-api";
});

builder.Services.AddKeycloakRealmAdminClient(
    "keycloak",
    "fiction-admin-client",
    "fiction");

// Configure OpenAPI documentation
builder.Services.AddOpenApiWithAuth(OpenIdConnectDefaults.AuthenticationScheme);

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

if (app.Environment.IsProduction())
{
    string[] trustedProxiesCidr = [.. (builder.Configuration["TrustedProxies"]
        ?? throw new InvalidOperationException("TrustedProxies configuration is required in production"))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    string[] allowedHosts = [.. (builder.Configuration["AllowedHosts"]
        ?? throw new InvalidOperationException("AllowedHosts configuration is required in production"))
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    ForwardedHeadersOptions options = new()
    {
        ForwardedHeaders = ForwardedHeaders.All,
        ForwardLimit = null,
        AllowedHosts = allowedHosts
    };

    foreach (var cidr in trustedProxiesCidr)
    {
        if (!System.Net.IPNetwork.TryParse(cidr, out var proxy)) continue;
        options.KnownIPNetworks.Add(proxy);
    }

    app.UseForwardedHeaders(options);
    app.UseCors();
}

// Configure authentication and authorization
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();

app.MapScalarApiReference(o => o
    .AddPreferredSecuritySchemes("OAuth2")
    .AddHttpAuthentication("JWT", scheme => scheme
        .WithDescription("JWT with fiction-api audience."))
    .AddAuthorizationCodeFlow("OAuth2", flow => flow
        .WithClientId("fiction-api-docs")
        .WithSelectedScopes("fiction_api"))
    .WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl));

// Map endpoints
app.MapEndpoints();
app.MapDefaultEndpoints();

await app.RunAsync();
