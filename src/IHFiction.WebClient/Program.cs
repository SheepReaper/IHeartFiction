using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Extensions;
using IHFiction.SharedWeb.Services;
using IHFiction.WebClient;
using IHFiction.WebClient.Components;

using Keycloak.AuthServices.Authorization;

using Markdig;
using IHFiction.SharedKernel.Infrastructure;

const string keycloakAuthenticationScheme = "Keycloak";

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpContextAccessor();

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
        {
            options.RequireHttpsMetadata = false;
        }
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
    {
        client.BaseAddress = new("https+http://fiction");
    })
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

builder.Services.AddSingleton(_ => VersionHelper.Get());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapStaticAssets();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(IHFiction.SharedWeb._Imports).Assembly);

app.MapGroup("authentication").MapLoginAndLogout(CookieAuthenticationDefaults.AuthenticationScheme, keycloakAuthenticationScheme);

app.MapDefaultEndpoints();

await app.RunAsync();
