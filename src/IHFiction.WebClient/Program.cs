using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using IHFiction.Data;
using IHFiction.Data.Contexts;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Extensions;
using IHFiction.SharedWeb.Services;
using IHFiction.WebClient;
using IHFiction.WebClient.Components;

using Keycloak.AuthServices.Authorization;

using Markdig;

const string keycloakAuthenticationScheme = "Keycloak";

var builder = WebApplication.CreateBuilder(args);

if (builder.Configuration["SecretsPath"] is string secretsPath)
    builder.Configuration.AddKeyPerFile(secretsPath, optional: true, reloadOnChange: true);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<FictionDbContext>(
    "fiction-db",
    configureDbContextOptions: (options) => options
        .UseNpgsql(options => options.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
        .UseSnakeCaseNamingConvention());

builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<FictionDbContext>()
    .SetApplicationName(builder.Environment.ApplicationName);

builder.Services
    .AddAuthentication(keycloakAuthenticationScheme)
    .AddKeycloakOpenIdConnect("keycloak", "fiction", keycloakAuthenticationScheme, options =>
    {
        options.ClientId = "fiction-frontend";
        options.Scope.Add("fiction_api");
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Resource = "fiction-api";

        options.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.PreferredUsername;

        if (builder.Environment.IsDevelopment())
            options.RequireHttpsMetadata = false;

        else if (builder.Configuration["OidcAuthority"] is string authority)
            options.Authority = authority;

    })
    .AddCookieWithOidcApiToken(CookieAuthenticationDefaults.AuthenticationScheme, keycloakAuthenticationScheme, 60);

builder.Services.AddKeycloakAuthorization(options =>
{
    options.RoleClaimType = ClaimTypes.Role;
    options.EnableRolesMapping = RolesClaimTransformationSource.All;
    options.Resource = "fiction-api";
});

builder.Services.AddAuthorizationBuilder();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<StoryEditorService>();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddTransient<AuthenticationHandler>();

builder.Services.AddHttpClient<IFictionApiClient, FictionApiClient>(client =>
    client.BaseAddress = builder.Environment.IsProduction()
        ? builder.Configuration.GetValue<Uri>("ApiBaseAddress")
        : new("https+http://fiction"))
        .AddHttpMessageHandler<AuthenticationHandler>();

builder.Services.AddTransient<AccountService>();
builder.Services.AddTransient<AuthorService>();
builder.Services.AddTransient<ChapterService>();
builder.Services.AddTransient<StoryService>();
builder.Services.AddTransient<BookService>();

// Configure global Markdown rendering options
builder.Services.AddSingleton(new MarkdownPipelineBuilder()
    .UseEmphasisExtras()
    .Build());

builder.Services.AddSingleton(VersionHelper.Get());

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

    app.UseHsts();
}

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
}

app.MapStaticAssets();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(IHFiction.SharedWeb._Imports).Assembly);

app.MapGroup("authentication")
    .MapLoginAndLogout(CookieAuthenticationDefaults.AuthenticationScheme, keycloakAuthenticationScheme);

app.MapDefaultEndpoints();

await app.RunAsync();
