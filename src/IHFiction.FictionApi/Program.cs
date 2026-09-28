using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Extensions;

[assembly: DbContext(typeof(FictionDbContext))]

AppContext.SetSwitch("Npgsql.EnableGss", false);

static bool IsBuildEnvironment() => Environment.CommandLine.Contains("GetDocument.Insider", StringComparison.OrdinalIgnoreCase);

var builder = WebApplication.CreateSlimBuilder(args);

// Slim builder disables https support, add it back in development
if (builder.Environment.IsDevelopment())
    builder.WebHost.UseKestrelHttpsConfiguration();

TimeProvider dateTime = TimeProvider.System;
var isBuildEnv = IsBuildEnvironment();

// Add Aspire service defaults (must be first)
builder.AddServiceDefaults();

// Add feature-based service registrations
builder.AddCoreApiServices(dateTime);
builder.AddPersistenceServices(dateTime, isBuildEnv);
builder.AddMessagingServices(isBuildEnv);
builder.AddSecurityServices(isBuildEnv);
builder.Services.AddOpenApiWithAuth(OpenIdConnectDefaults.AuthenticationScheme);
builder.AddApplicationServices();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseFictionApiPipeline(builder.Configuration);

await app.RunAsync();
