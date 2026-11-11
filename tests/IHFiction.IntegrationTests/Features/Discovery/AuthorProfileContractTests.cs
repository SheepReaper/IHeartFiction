using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IHFiction.IntegrationTests.Features.Discovery;

public sealed class AuthorProfileContractTests(IntegrationTestWebAppFactory factory) : IConfigureServices<AuthorProfileContractTests>
{
    [Fact]
    public async Task PublicProfile_ReturnsCreationDateAndOnlyPublicLinksRegardlessOfCaller()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredKeyedService<FictionDbContext>(nameof(AuthorProfileContractTests));
        await context.Database.MigrateAsync(cancellationToken);
        var author = new Author { Id = Ulid.Parse("01K67FE394KBYC4RAEXAB4YGKK"), Name = "Profile Author", UserId = Guid.NewGuid(), Profile = new Profile { Bio = "## Biography" } };
        context.Add(author);
        await context.SaveChangesAsync(cancellationToken);
        var created = author.CreatedAt;
        var updated = new DateTime(2026, 5, 6, 0, 0, 0, DateTimeKind.Utc);
        await context.Authors.Where(candidate => candidate.Id == author.Id).ExecuteUpdateAsync(setters => setters
            .SetProperty(candidate => candidate.UpdatedAt, updated), cancellationToken);

        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped(provider => provider.GetRequiredKeyedService<FictionDbContext>(nameof(AuthorProfileContractTests)));
            services.AddAuthentication(options => options.DefaultAuthenticateScheme = "ProfileTest")
                .AddScheme<AuthenticationSchemeOptions, ProfileAuthenticationHandler>("ProfileTest", _ => { });
        }));
        using var client = app.CreateClient();
        foreach (var identity in new[] { "anonymous", "owner", "other", "owner-without-role" })
        {
            client.DefaultRequestHeaders.Remove("X-Profile-User");
            client.DefaultRequestHeaders.Remove("X-Profile-Role");
            if (identity != "anonymous")
            {
                client.DefaultRequestHeaders.Add("X-Profile-User", identity == "other" ? Guid.NewGuid().ToString() : author.UserId.ToString());
                if (identity != "owner-without-role") client.DefaultRequestHeaders.Add("X-Profile-Role", "author");
            }
            foreach (var fields in new[] { "", "?Fields=createdAt,name" })
            {
                using var response = await client.GetAsync($"/authors/{author.Id}{fields}", cancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var root = document.RootElement;
                Assert.Equal(created, root.GetProperty("createdAt").GetDateTime());
                if (string.IsNullOrEmpty(fields))
                {
                    Assert.Equal(updated, root.GetProperty("updatedAt").GetDateTime());
                }
                else
                {
                    Assert.False(root.TryGetProperty("updatedAt", out _));
                }
                var links = root.GetProperty("links").EnumerateArray().ToArray();
                var self = Assert.Single(links, link => link.GetProperty("rel").GetString() == "self");
                Assert.Equal(new[] { "self", "stories" }, links.Select(link => link.GetProperty("rel").GetString()).Order());
                Assert.Equal("self", self.GetProperty("rel").GetString());
                Assert.Equal("GET", self.GetProperty("method").GetString());
                Assert.Equal($"/authors/{author.Id}", new Uri(self.GetProperty("href").GetString()!).AbsolutePath);
            }
        }
    }

    public sealed class ProfileAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Profile-User", out var user))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], Scheme.Name);
            if (Request.Headers.TryGetValue("X-Profile-Role", out var role)) identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "Implements IConfigureServices<T>.")]
    public static void ConfigureServices(IServiceCollection services)
        => services.AddKeyedTestFictionDbContext<AuthorProfileContractTests>(configurePendingModelWarning: false);
}
