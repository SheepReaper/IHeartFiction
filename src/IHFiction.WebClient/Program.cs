using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mime;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using IHFiction.Data;
using IHFiction.Data.Contexts;
using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Notifications;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Components.Disqus;
using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Extensions;
using IHFiction.SharedWeb.Services;
using IHFiction.SharedWeb.Sitemap;
using IHFiction.WebClient;
using IHFiction.WebClient.Components;

using Keycloak.AuthServices.Authorization;

using Markdig;

using Sidio.Sitemap.Blazor;
using Sidio.Sitemap.Core.Services;

const string keycloakAuthenticationScheme = "Keycloak";

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpContextAccessor();

builder.Services.AddOptions<WebPushOptions>()
    .Bind(builder.Configuration.GetSection("WebPush"))
    .Validate(options =>
        !string.IsNullOrWhiteSpace(options.PublicKey),
        "WebPush options must have PublicKey configured.");

builder.Services.AddOptions<SiteUrlOptions>()
    .Configure(options =>
    {
        var configuredBaseUrl = builder.Configuration["BaseUrl"];
        options.BaseUrl = Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var uri) ? uri : null;
    })
    .Validate(
        options => options.BaseUrl is { IsAbsoluteUri: true }
            && (options.BaseUrl.Scheme == Uri.UriSchemeHttps || options.BaseUrl.Scheme == Uri.UriSchemeHttp),
        "BaseUrl must be an absolute HTTP(S) URL.")
    .ValidateOnStart();

builder.Services.AddOptions<DisqusOptions>()
    .Bind(builder.Configuration.GetSection(DisqusOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ShortName), "Disqus ShortName must be configured.")
    .ValidateOnStart();

builder.Services.AddSingleton<IComponentBaseProvider, ComponentBaseProvider>();

builder.Services.AddRoutingCore()
    .Configure<RouteOptions>(options => options.SetParameterPolicy<UlidRouteConstraint>("ulid"));

builder.Services.AddDefaultSitemapServices<HttpContextBaseUrlProvider>()
    .AddCustomSitemapNodeProvider<DynamicSitemapNodeProvider>();

builder.AddNpgsqlDbContext<FictionDbContext>(
    "fiction-db",
    configureDbContextOptions: (options) => options
        .UseNpgsql(options => options.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Application))
        .UseSnakeCaseNamingConvention());

if (builder.Environment.IsProduction())
{
    builder.Services.AddDataProtection()
        .PersistKeysToDbContext<FictionDbContext>()
        .SetApplicationName(builder.Environment.ApplicationName);
}

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

        if (builder.Configuration["OidcAuthority"] is string authority)
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
builder.Services.AddScoped<ProtectedLocalStorage>();
builder.Services.AddScoped<BrowserProtectedStorageService>();
builder.Services.AddScoped<ReaderProgressService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<ViewPreferencesService>();
builder.Services.AddScoped<StoryEditorService>();
builder.Services.AddScoped<MetadataUrlService>();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        // HACK: Increase the maximum message size to handle large pastes in the content editor
        // This should be revisited to use chunking from the client side
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10 MB
    });

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache(options =>
{
    // Apply to middleware-generated sitemap response
    options.AddBasePolicy(policy => policy
        .With(ctx =>
            HttpMethods.IsGet(ctx.HttpContext.Request.Method) &&
            ctx.HttpContext.Request.Path.Equals("/sitemap.xml", StringComparison.OrdinalIgnoreCase))
        .Expire(TimeSpan.FromHours(1))
        .Tag("sitemap"));

    options.AddPolicy("Robots", policy => policy
        .Expire(TimeSpan.FromHours(6))
        .SetVaryByHost(false)
        .Tag("robots"));
});

builder.Services.AddTransient<AuthenticationHandler>();

builder.Services.AddHttpClient<FictionApiClient>(client =>
    client.BaseAddress = builder.Environment.IsProduction()
        ? builder.Configuration.GetValue<Uri>("ApiBaseAddress")
        : new("https+http://fiction"))
        .AddHttpMessageHandler<AuthenticationHandler>();

builder.Services.AddTransient<IFictionApiClient>(services => services.GetRequiredService<FictionApiClient>());

builder.Services.AddTransient<AccountService>();
builder.Services.AddTransient<AuthorService>();
builder.Services.AddTransient<BookService>();
builder.Services.AddTransient<ChapterService>();
builder.Services.AddTransient<NotificationService>();
builder.Services.AddTransient<StoryService>();
builder.Services.AddTransient<WorkService>();

// Configure global Markdown rendering options
builder.Services.AddSingleton(new MarkdownPipelineBuilder()
    .UseEmphasisExtras()
    .Build());

builder.Services.AddSingleton(VersionHelper.Get());

// Register LoaderService for global loading spinner
builder.Services.AddScoped<LoaderService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

    app.UseHsts();
}

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

    app.UseCsp();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals(
        "/_content/IHFiction.SharedWeb/js/service-worker.js",
        StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Headers["Service-Worker-Allowed"] = "/";
    }

    await next();
});

app.UseRequestTimeouts();
app.UseOutputCache();

app.MapStaticAssets();

app.MapGet("/stories/{storyId:ulid}/read", (Ulid storyId) => Results.Redirect($"/read/{storyId}", permanent: true));

app.MapGet("/stories/{storyId:ulid}/chapters/{chapterId:ulid}", (Ulid storyId, Ulid chapterId) => Results.Redirect($"/read/{chapterId}", permanent: true));

app.MapGet("/stories/{id}/cover", async Task<IResult> (
    string id,
    FictionApiClient fictionApiClient,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (!Ulid.TryParse(id, out _))
        return TypedResults.NotFound();

    if (fictionApiClient is not FictionApiClient client)
        return TypedResults.Problem("Invalid API client registration.", statusCode: StatusCodes.Status500InternalServerError);

    using var response = await client.GetStoryCoverResponseAsync(id, cancellationToken);

    if (response.StatusCode == HttpStatusCode.NotFound)
        return TypedResults.NotFound();

    if (response.StatusCode == HttpStatusCode.Forbidden)
        return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

    if (!response.IsSuccessStatusCode)
        return TypedResults.StatusCode((int)response.StatusCode);

    var contentType = response.Content.Headers.ContentType?.ToString() ?? MediaTypeNames.Application.Octet;

    if (response.Headers.ETag is { } etag)
        httpContext.Response.Headers.ETag = etag.ToString();

    if (response.Headers.CacheControl is { } cacheControl)
        httpContext.Response.Headers.CacheControl = cacheControl.ToString();

    if (response.Content.Headers.LastModified is { } lastModified)
        httpContext.Response.Headers.LastModified = lastModified.ToString("R");

    var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
    return TypedResults.File(content, contentType);
});

app.MapGroup("authentication")
    .MapLoginAndLogout(CookieAuthenticationDefaults.AuthenticationScheme, keycloakAuthenticationScheme);

app.MapCspReportingEndpoint();

app.MapGet("/robots.txt", (IOptions<SiteUrlOptions> siteUrl, HttpContext ctx) =>
{
    var baseUrl = siteUrl.Value.BaseUrl!.ToString().TrimEnd('/');

    ctx.Response.Headers.CacheControl = "public, max-age=21600, s-maxage=21600";

    var body = $"Sitemap: {baseUrl}/sitemap.xml\n";

    return Results.Text(body, MediaTypeNames.Text.Plain);
}).CacheOutput("Robots");

app.UseSitemap();

app.MapMethods("/uptime", [HttpMethods.Head, HttpMethods.Get], () => Results.Ok());

app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode(options => options.ContentSecurityFrameAncestorsPolicy = null) // This is set in the CSP above
    .AddAdditionalAssemblies(typeof(IHFiction.SharedWeb._Imports).Assembly);

await app.RunAsync();
