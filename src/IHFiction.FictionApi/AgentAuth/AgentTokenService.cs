using System.Security.Claims;
using System.Security.Cryptography;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IHFiction.FictionApi.AgentAuth;

internal sealed class AgentTokenService : IDisposable
{
    public const string AuthenticationScheme = "AgentBearer";
    public const string RegistrationIdClaim = "agent_registration_id";
    public const string TokenKindClaim = "token_kind";
    public const string ServiceAssertionKind = "identity_assertion";
    public const string AccessTokenKind = "access_token";

    private readonly AgentAuthOptions _options;
    private readonly ECDsaSecurityKey _securityKey;
    private readonly List<(ECDsa Algorithm, ECDsaSecurityKey Key)> _verificationKeys = [];
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };

    public AgentTokenService(IOptions<AgentAuthOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.SigningKeyPem))
            throw new InvalidOperationException("AgentAuth:SigningKeyPem must be configured.");

        var activeAlgorithm = ECDsa.Create();
        activeAlgorithm.ImportFromPem(_options.SigningKeyPem);
        _securityKey = new ECDsaSecurityKey(activeAlgorithm) { KeyId = _options.SigningKeyId };
        _verificationKeys.Add((activeAlgorithm, _securityKey));

        foreach (var previous in _options.PreviousVerificationKeys)
        {
            var algorithm = ECDsa.Create();
            algorithm.ImportFromPem(previous.PublicKeyPem);
            _verificationKeys.Add((algorithm, new ECDsaSecurityKey(algorithm) { KeyId = previous.KeyId }));
        }
    }

    public string CreateServiceAssertion(Ulid registrationId, Guid userId, DateTime now, out DateTime expiresAt) =>
        CreateToken(
            registrationId,
            registrationId.ToString(),
            ServiceAssertionKind,
            "agent.exchange",
            _options.Issuer?.AbsoluteUri.TrimEnd('/') ?? throw new InvalidOperationException("AgentAuth:Issuer must be configured."),
            "oauth-id-jag+jwt",
            now,
            _options.ServiceAssertionLifetime,
            out expiresAt,
            additionalClaims: new Dictionary<string, object> { ["user_id"] = userId.ToString() });

    public string CreateAccessToken(Ulid registrationId, Guid userId, DateTime now, out string jti, out DateTime expiresAt)
    {
        jti = Guid.NewGuid().ToString("N");
        return CreateToken(
            registrationId,
            userId.ToString(),
            AccessTokenKind,
            "agent.read profile.read",
            _options.Audience,
            "at+jwt",
            now,
            _options.AccessTokenLifetime,
            out expiresAt,
            jti);
    }

    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidIssuer = _options.Issuer?.AbsoluteUri.TrimEnd('/'),
        ValidAudience = _options.Audience,
        IssuerSigningKeys = _verificationKeys.Select(item => item.Key),
        ValidateIssuerSigningKey = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidTypes = ["at+jwt"],
        NameClaimType = ClaimTypes.NameIdentifier,
    };

    public TokenValidationParameters CreateServiceAssertionValidationParameters()
    {
        var parameters = CreateValidationParameters();
        parameters.ValidAudience = _options.Issuer?.AbsoluteUri.TrimEnd('/');
        parameters.ValidTypes = ["oauth-id-jag+jwt"];
        return parameters;
    }

    public object CreateJwksDocument()
    {
        return new
        {
            keys = _verificationKeys.Select(item =>
            {
                var parameters = item.Algorithm.ExportParameters(false);
                return new
                {
                    kty = "EC",
                    use = "sig",
                    crv = "P-256",
                    kid = item.Key.KeyId,
                    alg = SecurityAlgorithms.EcdsaSha256,
                    x = Base64UrlEncoder.Encode(parameters.Q.X),
                    y = Base64UrlEncoder.Encode(parameters.Q.Y),
                };
            }).ToArray(),
        };
    }

    private string CreateToken(
        Ulid registrationId,
        string subject,
        string kind,
        string scope,
        string audience,
        string tokenType,
        DateTime now,
        TimeSpan lifetime,
        out DateTime expiresAt,
        string? jti = null,
        IReadOnlyDictionary<string, object>? additionalClaims = null)
    {
        var issuer = _options.Issuer?.AbsoluteUri.TrimEnd('/')
            ?? throw new InvalidOperationException("AgentAuth:Issuer must be configured.");
        expiresAt = now.Add(lifetime);
        jti ??= Guid.NewGuid().ToString("N");

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            TokenType = tokenType,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(_securityKey, SecurityAlgorithms.EcdsaSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = subject,
                [JwtRegisteredClaimNames.Jti] = jti,
                [RegistrationIdClaim] = registrationId.ToString(),
                [TokenKindClaim] = kind,
                ["scope"] = scope,
            },
        };

        if (additionalClaims is not null)
        {
            foreach (var claim in additionalClaims)
                descriptor.Claims[claim.Key] = claim.Value;
        }

        return _handler.CreateToken(descriptor);
    }

    public void Dispose()
    {
        foreach (var item in _verificationKeys)
            item.Algorithm.Dispose();
    }
}
