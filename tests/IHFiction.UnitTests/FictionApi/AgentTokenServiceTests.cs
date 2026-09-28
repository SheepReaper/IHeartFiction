using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;

using FluentAssertions;

using IHFiction.FictionApi.AgentAuth;

using Microsoft.Extensions.Options;

namespace IHFiction.UnitTests.FictionApi;

public sealed class AgentTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_UsesAgentIssuerAudienceAndReadOnlyScopes()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var service = new AgentTokenService(Options.Create(new AgentAuthOptions
        {
            Issuer = new Uri("https://api.example.test"),
            Audience = "fiction-api",
            SigningKeyPem = key.ExportECPrivateKeyPem(),
            SigningKeyId = "test-key",
        }));
        var registrationId = Ulid.NewUlid();
        var userId = Guid.NewGuid();

        var encoded = service.CreateAccessToken(
            registrationId,
            userId,
            DateTime.UtcNow,
            out var jti,
            out _);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(encoded);
        token.Issuer.Should().Be("https://api.example.test");
        token.Audiences.Should().ContainSingle("fiction-api");
        token.Claims.Should().Contain(x => x.Type == JwtRegisteredClaimNames.Sub && x.Value == userId.ToString());
        token.Claims.Should().Contain(x => x.Type == JwtRegisteredClaimNames.Jti && x.Value == jti);
        token.Claims.Should().Contain(x => x.Type == AgentTokenService.RegistrationIdClaim && x.Value == registrationId.ToString());
        token.Claims.Should().Contain(x => x.Type == "scope" && x.Value == "agent.read profile.read");
    }

    [Fact]
    public void CreateJwksDocument_DoesNotExposePrivateKeyMaterial()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var service = new AgentTokenService(Options.Create(new AgentAuthOptions
        {
            Issuer = new Uri("https://api.example.test"),
            SigningKeyPem = key.ExportECPrivateKeyPem(),
        }));

        var json = JsonSerializer.Serialize(service.CreateJwksDocument());

        json.Should().Contain("\"kty\":\"EC\"");
        json.Should().Contain("\"x\":");
        json.Should().Contain("\"y\":");
        json.Should().NotContain("\"d\":");
    }

    [Fact]
    public void CreateServiceAssertion_UsesProtocolTokenTypeAndRegistrationSubject()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var service = new AgentTokenService(Options.Create(new AgentAuthOptions
        {
            Issuer = new Uri("https://api.example.test"),
            Audience = "fiction-api",
            SigningKeyPem = key.ExportECPrivateKeyPem(),
            SigningKeyId = "test-key",
        }));
        var registrationId = Ulid.NewUlid();

        var encoded = service.CreateServiceAssertion(
            registrationId,
            Guid.NewGuid(),
            DateTime.UtcNow,
            out _);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(encoded);
        token.Header.Typ.Should().Be("oauth-id-jag+jwt");
        token.Subject.Should().Be(registrationId.ToString());
        token.Audiences.Should().ContainSingle("https://api.example.test");
    }
}
