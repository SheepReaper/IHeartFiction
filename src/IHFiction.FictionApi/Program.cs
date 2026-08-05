using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using Cysharp.Serialization.Json;

using IHFiction.Data;
using IHFiction.Data.Contexts;
using IHFiction.Data.Infrastructure;
using IHFiction.Data.Stories.Domain;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;
using IHFiction.FictionApi.Infrastructure;
using IHFiction.FictionApi.Notifications;
using IHFiction.FictionApi.Stories;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Markdown;
using IHFiction.SharedKernel.Notifications;

using JasperFx.CodeGeneration.Model;
using JasperFx.Resources;

using Keycloak.AuthServices.Authorization;

using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

using Scalar.AspNetCore;

using StackExchange.Redis;

using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.Redis;
using Wolverine.Persistence;

[assembly: DbContext(typeof(FictionDbContext))]

AppContext.SetSwitch("Npgsql.EnableGss", false);

static bool IsBuildEnvironment() => Environment.CommandLine.Contains("GetDocument.Insider", StringComparison.OrdinalIgnoreCase);

var builder = WebApplication.CreateSlimBuilder(args);

// Slim builder disables https support, add it back in development
if (builder.Environment.IsDevelopment())
    builder.WebHost.UseKestrelHttpsConfiguration();

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

builder.Services.AddRoutingCore()
    .Configure<RouteOptions>(options => options.SetParameterPolicy<UlidRouteConstraint>("ulid"));

builder.Services.AddHttpContextAccessor();
builder.Services.AddRateLimiter(options => options.AddPolicy("qualified-reads", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        })));

// Configure JSON serialization for AOT compatibility
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = builder.Environment.IsDevelopment();

    // Add custom converters (these work with AOT)
    options.SerializerOptions.Converters.Add(new UlidJsonConverter());
    options.SerializerOptions.Converters.Add(new ObjectIdJsonConverter());
    options.SerializerOptions.Converters.Add(new LinkedConverterFactory());

    // Use source-generated JSON context for AOT compatibility
    // Combine with default resolver to support types not yet in the context
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, FictionApiJsonSerializerContext.Default);
});

if (!IsBuildEnvironment() && builder.Environment.IsProduction())
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
builder.Services.AddSingleton(FictionApiJsonSerializerContext.Default);
builder.Services.Configure<MarkdownOptions>(builder.Configuration.GetSection(MarkdownOptions.SectionName));
builder.Services.Configure<WebPushOptions>(builder.Configuration.GetSection("WebPush"));

// Configure database connections
builder.AddNpgsqlDbContext<FictionDbContext>(
    "fiction-db",
    configureDbContextOptions: (options) => options
        .UseNpgsql(options => options
            .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application)
            .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
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

builder.AddRedisClient("redis");

if (!IsBuildEnvironment())
{
    // Resource setup is a blocking Wolverine hosted service. Gate it on Redis readiness so
    // a rolling Swarm restart cannot terminate the API while Redis is being rescheduled.
    builder.Services.AddHostedService<RedisStartupReadinessService>();

    var fictionDbConnectionString = builder.Configuration.GetConnectionString("fiction-db")
        ?? throw new InvalidOperationException("The fiction-db connection string is required for Wolverine persistence.");
    // var redisConnectionString = builder.Configuration.GetConnectionString("redis")
    //     ?? throw new InvalidOperationException("The redis connection string is required for Wolverine transport.");

    builder.Host.UseWolverine(opts =>
    {
        // NotificationFanoutHandler is internal — Wolverine only scans exported (public) types by default,
        // so we must register it explicitly. Skip during OpenAPI doc generation (build env).
        opts.Discovery.IncludeType<NotificationFanoutHandler>();

        opts.CodeGeneration.AlwaysUseServiceLocationFor<FictionDbContext>();
        opts.ServiceLocationPolicy = ServiceLocationPolicy.NotAllowed;

        // PostgreSQL is the durable store for Wolverine's inbox, outbox, and
        // durable local queues. Redis Streams is the cross-instance transport.
        opts.PersistMessagesWithPostgresql(fictionDbConnectionString, Schemas.Wolverine);
        // Handlers that need resilient multi-operation transactions create them
        // through EF's execution strategy. Lightweight mode avoids opening an
        // unsupported user transaction around those handlers.
        opts.UseEntityFrameworkCoreTransactions(TransactionMiddlewareMode.Lightweight);

        opts.UseRedisTransport((sp) => sp.GetRequiredService<IConnectionMultiplexer>())
            .AutoProvision()
            .ConfigureDefaultConsumerName((runtime, _) => $"{runtime.Options.ServiceName}-{runtime.DurabilitySettings.AssignedNodeNumber}")
            .DeleteStreamEntryOnAck(true);

        const string notificationStream = "ihfiction-notifications";
        opts.PublishMessage<StoryPublishedNotificationRequested>()
            .ToRedisStream(notificationStream)
            .UseDurableOutbox();
        opts.PublishMessage<ChapterPublishedNotificationRequested>()
            .ToRedisStream(notificationStream)
            .UseDurableOutbox();

        opts.ListenToRedisStream(notificationStream, "fiction-notification-fanout")
            .StartFromBeginning()
            .EnableNativeDeadLetterQueue()
            .UseDurableInbox();

        const string workReadStream = "ihfiction-work-reads";
        opts.PublishMessage<RecordWorkReadRequested>()
            .ToRedisStream(workReadStream)
            .UseDurableOutbox();
        opts.ListenToRedisStream(workReadStream, "fiction-work-read-recorder")
            .StartFromBeginning()
            .EnableNativeDeadLetterQueue()
            .UseDurableInbox();

        // Any local queues added later inherit PostgreSQL durability instead of
        // silently becoming process-memory-only queues.
        opts.Policies.UseDurableLocalQueues();

        // Wolverine 6.x provisions its own PostgreSQL envelope tables and the
        // Redis stream/consumer group through the unified resource setup model.
        opts.Services.AddResourceSetupOnStartup();
    });
}

builder.Services.AddSingleton(services => services
    .GetRequiredService<IMongoDatabase>()
    .GetCollection<WorkBody>("works"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddKeycloakJwtBearer("keycloak", realm: "fiction", JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Audience = "fiction-api";
        options.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.PreferredUsername;

        if (builder.Environment.IsDevelopment())
            options.RequireHttpsMetadata = false;

        // Allow explicit authority override from configuration (e.g. to force HTTP endpoint in development)
        if (builder.Configuration["OidcAuthority"] is string authority)
            options.Authority = authority;

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

        options.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.PreferredUsername;

        if (builder.Environment.IsDevelopment() || IsBuildEnvironment())
        {
            options.RequireHttpsMetadata = false;
        }

        if (builder.Configuration["OidcAuthority"] is string authority)
            options.Authority = authority;

    });

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Wolverine");
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
    clientId: "fiction-admin-client",
    realm: "fiction");

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

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache();

var app = builder.Build();

// Configure middleware pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

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
else
{
    // In production we use a reverse proxy that handles TLS termination
    // Not normally needed in development, but Keycloak may behave strangely without additional configuration
    app.UseHttpsRedirection();
}

app.UseRequestTimeouts();
app.UseOutputCache();

// Configure authentication and authorization
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapOpenApi();

app.MapScalarApiReference(o =>
{
    // Initialize authentication if needed
    o.Authentication ??= new();
    o.Authentication.PreferredSecuritySchemes = ["OAuth2"];

    o.AddHttpAuthentication("JWT", scheme => scheme
        .WithDescription("JWT with fiction-api audience."))
    .AddAuthorizationCodeFlow("OAuth2", flow => flow
        .WithClientId("fiction-api-docs")
        .WithSelectedScopes("fiction_api"))
    .WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl);
});

// Map endpoints

app.MapEndpoints();
app.MapDefaultEndpoints();

if (builder.Configuration["ApiBaseAddress"] is string apiBaseAddress) app.Use((context, next) =>
{
    context.Response.Headers.Append("Content-Security-Policy",
        "frame-ancestors 'self'" + $" {apiBaseAddress.TrimEnd('/')}");

    return next(context);
});

await app.RunAsync();
