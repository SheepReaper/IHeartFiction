using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using IHFiction.FictionApi.Features.AgentAccess;
using IHFiction.FictionApi.Infrastructure;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IHFiction.IntegrationTests.Features.AgentAccess;

public sealed class AgentAuthEndpointTests(IntegrationTestWebAppFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/.well-known/oauth-protected-resource")]
    [InlineData("/.well-known/oauth-authorization-server")]
    [InlineData("/.well-known/jwks.json")]
    public async Task Discovery_PreservesReadOnlyMetadataAndPublicKeys(string path)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = document.RootElement;
        var issuer = factory.Services.GetRequiredService<IOptions<AgentAuthOptions>>().Value.Issuer!.AbsoluteUri.TrimEnd('/');
        if (path.EndsWith("oauth-protected-resource", StringComparison.Ordinal))
        {
            var resource = factory.Services.GetRequiredService<IOptions<BaseUrlOptions>>().Value.BaseUrl!.AbsoluteUri.TrimEnd('/');
            Assert.Equal(resource, root.GetProperty("resource").GetString());
            Assert.Equal(issuer, Assert.Single(root.GetProperty("authorization_servers").EnumerateArray()).GetString());
            Assert.Equal(["agent.read", "profile.read"], root.GetProperty("scopes_supported").EnumerateArray().Select(scope => scope.GetString()));
        }
        else if (path.EndsWith("oauth-authorization-server", StringComparison.Ordinal))
        {
            Assert.Equal(issuer, root.GetProperty("issuer").GetString());
            Assert.Equal($"{issuer}/oauth2/token", root.GetProperty("token_endpoint").GetString());
            Assert.Equal($"{issuer}/oauth2/revoke", root.GetProperty("revocation_endpoint").GetString());
            Assert.Equal($"{issuer}/agent/identity", root.GetProperty("agent_auth").GetProperty("identity_endpoint").GetString());
            Assert.Equal(["agent.read", "profile.read"], root.GetProperty("scopes_supported").EnumerateArray().Select(scope => scope.GetString()));
        }
        else
        {
            var keys = root.GetProperty("keys").EnumerateArray().ToArray();
            Assert.NotEmpty(keys);
            Assert.All(keys, key =>
            {
                Assert.False(key.TryGetProperty("d", out _));
                Assert.False(string.IsNullOrEmpty(key.GetProperty("kid").GetString()));
            });
        }
    }

    [Theory]
    [InlineData("/.well-known/oauth-protected-resource")]
    [InlineData("/.well-known/oauth-authorization-server")]
    [InlineData("/auth.md")]
    public async Task Discovery_HeadPreservesEmptySuccessfulResponse(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, path);
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        if (path.EndsWith("oauth-protected-resource", StringComparison.Ordinal))
            Assert.Contains("rel=\"oauth-protected-resource\"", Assert.Single(response.Headers.GetValues("Link")), StringComparison.Ordinal);
        if (path == "/auth.md")
            Assert.Equal("text/markdown", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RegisterIdentity_RejectsUnsupportedTypeWithProtocolError()
    {
        using var response = await _client.PostAsJsonAsync("/agent/identity",
            new { type = "unsupported", assertion_type = "unsupported", assertion = "invalid" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("invalid_request", document.RootElement.GetProperty("error").GetString());
        Assert.Equal("Only identity_assertion registration is supported.", document.RootElement.GetProperty("error_description").GetString());
    }

    [Fact]
    public async Task ConfirmClaim_RequiresBearerAuthentication()
    {
        using var response = await _client.PostAsJsonAsync("/agent/identity/claim/confirm",
            new { userCode = "123456" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var endpoint = FindEndpoint("/agent/identity/claim/confirm");
        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), policy => policy.AuthenticationSchemes == "Bearer");
    }

    [Theory]
    [InlineData("/agent/identity")]
    [InlineData("/agent/identity/claim")]
    [InlineData("/agent/identity/claim/confirm")]
    [InlineData("/oauth2/token")]
    [InlineData("/oauth2/revoke")]
    [InlineData("/agent/event/notify")]
    public void ProtocolEndpoints_RetainAuthRateLimit(string path)
    {
        Assert.Equal(RateLimitPolicies.Auth, FindEndpoint(path).Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Theory]
    [InlineData("/oauth2/token")]
    [InlineData("/oauth2/revoke")]
    public void TokenForms_RetainAntiforgeryExemption(string path)
    {
        Assert.False(FindEndpoint(path).Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation);
    }

    private RouteEndpoint FindEndpoint(string path) => Assert.Single(
        factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>(),
        endpoint => endpoint.RoutePattern.RawText == path);
}
