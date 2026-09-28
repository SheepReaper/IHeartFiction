using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using FluentAssertions;

using IHFiction.FictionApi.AgentAuth;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

namespace IHFiction.UnitTests.FictionApi;

public sealed class AgentAssertionValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidateAsync_AcceptsAllowlistedFreshVerifiedIdentity()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var validator = CreateValidator(key, Now);
        var assertion = CreateAssertion(key, Now, "https://api.example.test");

        var identity = await validator.ValidateAsync(
            "urn:ietf:params:oauth:token-type:id-jag",
            assertion,
            TestContext.Current.CancellationToken);

        identity.Issuer.Should().Be("https://provider.example.test");
        identity.Subject.Should().Be("provider-user-1");
        identity.VerifiedEmail.Should().Be("reader@example.test");
    }

    [Fact]
    public async Task ValidateAsync_RejectsWrongAudienceWithProtocolError()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var validator = CreateValidator(key, Now);
        var assertion = CreateAssertion(key, Now, "https://other.example.test");

        var act = () => validator.ValidateAsync(
            "urn:ietf:params:oauth:token-type:id-jag",
            assertion,
            TestContext.Current.CancellationToken);

        var exception = await act.Should().ThrowAsync<AgentAssertionValidationException>();
        exception.Which.Error.Should().Be("invalid_audience");
    }

    [Fact]
    public async Task ValidateAsync_RejectsStaleAuthentication()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var validator = CreateValidator(key, Now);
        var assertion = CreateAssertion(key, Now, "https://api.example.test", Now.AddHours(-2));

        var act = () => validator.ValidateAsync(
            "urn:ietf:params:oauth:token-type:id-jag",
            assertion,
            TestContext.Current.CancellationToken);

        var exception = await act.Should().ThrowAsync<AgentAssertionValidationException>();
        exception.Which.Error.Should().Be("login_required");
    }

    private static AgentAssertionValidator CreateValidator(ECDsa key, DateTimeOffset now)
    {
        var publicParameters = key.ExportParameters(false);
        var jwks = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "EC",
                    use = "sig",
                    crv = "P-256",
                    kid = "provider-key",
                    alg = "ES256",
                    x = Base64UrlEncoder.Encode(publicParameters.Q.X),
                    y = Base64UrlEncoder.Encode(publicParameters.Q.Y),
                },
            },
        });
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(new JsonHandler(jwks)));
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new AgentAssertionValidator(
            Options.Create(new AgentAuthOptions
            {
                Issuer = new Uri("https://api.example.test"),
                ProviderAssertionMaxAge = TimeSpan.FromHours(1),
                TrustedProviders =
                [
                    new TrustedAgentProviderOptions
                    {
                        Issuer = new Uri("https://provider.example.test"),
                        JwksUri = new Uri("https://provider.example.test/.well-known/jwks.json"),
                        ClientIds = ["trusted-agent"],
                        SigningAlgorithms = ["ES256"],
                    },
                ],
            }),
            factory,
            cache,
            new FixedTimeProvider(now));
    }

    private static string CreateAssertion(
        ECDsa key,
        DateTimeOffset now,
        string audience,
        DateTimeOffset? authTime = null)
    {
        var securityKey = new ECDsaSecurityKey(key) { KeyId = "provider-key" };
        return new JwtSecurityTokenHandler { MapInboundClaims = false }.CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Issuer = "https://provider.example.test",
            Audience = audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(10).UtcDateTime,
            TokenType = "oauth-id-jag+jwt",
            SigningCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = "provider-user-1",
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
                ["client_id"] = "trusted-agent",
                [JwtRegisteredClaimNames.AuthTime] = (authTime ?? now).ToUnixTimeSeconds(),
                [JwtRegisteredClaimNames.Email] = "reader@example.test",
                ["email_verified"] = true,
            },
        });
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
