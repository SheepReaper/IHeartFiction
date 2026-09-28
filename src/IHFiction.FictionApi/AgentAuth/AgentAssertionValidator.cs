using System.Security.Claims;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IHFiction.FictionApi.AgentAuth;

internal sealed record ValidatedAgentIdentity(
    string Issuer,
    string Subject,
    string ClientId,
    string AssertionJti,
    string VerifiedEmail,
    DateTime ExpiresAt);

internal sealed record ValidatedAgentRevocation(string Issuer, string Subject, string EventJti, DateTime ExpiresAt);

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "Standard exception constructors are required by exception design analyzers.")]
public sealed class AgentAssertionValidationException : Exception
{
    public AgentAssertionValidationException() { }
    public AgentAssertionValidationException(string message) : base(message) { }
    public AgentAssertionValidationException(string message, Exception innerException) : base(message, innerException) { }
    public AgentAssertionValidationException(string error, string message) : base(message) => Error = error;
    public string Error { get; } = "invalid_request";
}

internal sealed class AgentAssertionValidator(
    IOptions<AgentAuthOptions> options,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    TimeProvider timeProvider)
{
    private const string IdJagType = "oauth-id-jag+jwt";
    private const string IdJagAssertionType = "urn:ietf:params:oauth:token-type:id-jag";
    private readonly AgentAuthOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };

    public async Task<ValidatedAgentIdentity> ValidateAsync(
        string assertionType,
        string assertion,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(assertionType, IdJagAssertionType, StringComparison.Ordinal))
            throw new AgentAssertionValidationException("invalid_request", "Unsupported assertion_type.");

        JsonWebToken unvalidated;
        try
        {
            unvalidated = _handler.ReadJsonWebToken(assertion);
        }
        catch (ArgumentException exception)
        {
            throw new AgentAssertionValidationException("invalid_request", "The assertion is not a valid JWT.") { Source = exception.Source };
        }

        var provider = _options.TrustedProviders.SingleOrDefault(candidate =>
            candidate.Issuer is not null
            && string.Equals(candidate.Issuer.AbsoluteUri.TrimEnd('/'), unvalidated.Issuer.TrimEnd('/'), StringComparison.Ordinal));
        if (provider is null)
            throw new AgentAssertionValidationException("invalid_issuer", "The assertion issuer is not trusted.");

        if (!provider.SigningAlgorithms.Contains(unvalidated.Alg, StringComparer.Ordinal))
            throw new AgentAssertionValidationException("invalid_signature", "The assertion signing algorithm is not allowed.");

        var signingKeys = await GetSigningKeysAsync(provider, cancellationToken);
        var validation = await _handler.ValidateTokenAsync(assertion, new TokenValidationParameters
            {
                ValidIssuer = provider.Issuer!.AbsoluteUri.TrimEnd('/'),
                ValidAudience = _options.Issuer?.AbsoluteUri.TrimEnd('/'),
                IssuerSigningKeys = signingKeys,
                ValidAlgorithms = provider.SigningAlgorithms,
                ValidTypes = [IdJagType],
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                LifetimeValidator = ValidateLifetime,
            });
        if (!validation.IsValid || validation.ClaimsIdentity is null || validation.SecurityToken is null)
            throw CreateValidationException(validation.Exception, "assertion");
        var principal = new ClaimsPrincipal(validation.ClaimsIdentity);

        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var clientId = principal.FindFirstValue("client_id");
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(jti) || string.IsNullOrWhiteSpace(clientId))
            throw new AgentAssertionValidationException("invalid_request", "The assertion is missing sub, jti, or client_id.");

        if (provider.ClientIds.Count > 0 && !provider.ClientIds.Contains(clientId, StringComparer.Ordinal))
            throw new AgentAssertionValidationException("invalid_client_id", "The assertion client_id is not trusted.");

        var now = timeProvider.GetUtcNow();
        var issuedAtValue = principal.FindFirstValue(JwtRegisteredClaimNames.Iat);
        if (!long.TryParse(issuedAtValue, out var issuedAtSeconds))
            throw new AgentAssertionValidationException("invalid_request", "The assertion is missing iat.");
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds);
        var expiresAt = new DateTimeOffset(validation.SecurityToken.ValidTo, TimeSpan.Zero);
        if (issuedAt > now.AddMinutes(1)
            || expiresAt - issuedAt > _options.ProviderAssertionMaxAge)
            throw new AgentAssertionValidationException("expired", "The assertion validity window is not acceptable.");

        var authTimeValue = principal.FindFirstValue(JwtRegisteredClaimNames.AuthTime);
        if (!long.TryParse(authTimeValue, out var authTimeSeconds)
            || DateTimeOffset.FromUnixTimeSeconds(authTimeSeconds) > now.AddMinutes(1)
            || now - DateTimeOffset.FromUnixTimeSeconds(authTimeSeconds) > _options.ProviderAssertionMaxAge)
            throw new AgentAssertionValidationException("login_required", "The provider authentication is too old.");

        var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email);
        var emailVerified = principal.FindFirstValue("email_verified");
        if (string.IsNullOrWhiteSpace(email) || !bool.TryParse(emailVerified, out var verified) || !verified)
            throw new AgentAssertionValidationException("missing_verified_email", "A verified email is required.");

        return new ValidatedAgentIdentity(
            provider.Issuer.AbsoluteUri.TrimEnd('/'),
            subject,
            clientId,
            jti,
            email.Trim(),
            validation.SecurityToken.ValidTo);
    }

    public async Task<ValidatedAgentRevocation> ValidateRevocationEventAsync(
        string securityEventToken,
        CancellationToken cancellationToken)
    {
        JsonWebToken unvalidated;
        try
        {
            unvalidated = _handler.ReadJsonWebToken(securityEventToken);
        }
        catch (ArgumentException)
        {
            throw new AgentAssertionValidationException("invalid_request", "The security event is not a valid JWT.");
        }

        var provider = _options.TrustedProviders.SingleOrDefault(candidate =>
            candidate.Issuer is not null
            && string.Equals(candidate.Issuer.AbsoluteUri.TrimEnd('/'), unvalidated.Issuer.TrimEnd('/'), StringComparison.Ordinal))
            ?? throw new AgentAssertionValidationException("invalid_issuer", "The security event issuer is not trusted.");
        var keys = await GetSigningKeysAsync(provider, cancellationToken);
        var validation = await _handler.ValidateTokenAsync(securityEventToken, new TokenValidationParameters
            {
                ValidIssuer = provider.Issuer!.AbsoluteUri.TrimEnd('/'),
                ValidAudience = _options.Issuer?.AbsoluteUri.TrimEnd('/'),
                IssuerSigningKeys = keys,
                ValidAlgorithms = provider.SigningAlgorithms,
                ValidTypes = ["secevent+jwt"],
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromMinutes(1),
                LifetimeValidator = ValidateLifetime,
            });
        if (!validation.IsValid || validation.ClaimsIdentity is null || validation.SecurityToken is null)
            throw CreateValidationException(validation.Exception, "security event");
        var principal = new ClaimsPrincipal(validation.ClaimsIdentity);

        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var events = principal.FindFirstValue("events");
        var issuedAtValue = principal.FindFirstValue(JwtRegisteredClaimNames.Iat);
        const string revokedEvent = "https://schemas.workos.com/events/agent/auth/identity/assertion/revoked";
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(jti)
            || !long.TryParse(issuedAtValue, out var issuedAtSeconds)
            || string.IsNullOrWhiteSpace(events) || !events.Contains(revokedEvent, StringComparison.Ordinal))
            throw new AgentAssertionValidationException("invalid_request", "The required revocation event claims are missing.");

        var now = timeProvider.GetUtcNow();
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds);
        if (issuedAt > now.AddMinutes(1) || now - issuedAt > _options.ProviderAssertionMaxAge)
            throw new AgentAssertionValidationException("expired", "The security event is outside the accepted freshness window.");

        var expiresAt = validation.SecurityToken.ValidTo == DateTime.MinValue
            ? now.Add(_options.ProviderAssertionMaxAge).UtcDateTime
            : validation.SecurityToken.ValidTo;
        return new(provider.Issuer.AbsoluteUri.TrimEnd('/'), subject, jti, expiresAt);
    }

    private async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(
        TrustedAgentProviderOptions provider,
        CancellationToken cancellationToken)
    {
        if (provider.Issuer is null || provider.JwksUri is null)
            throw new AgentAssertionValidationException("invalid_issuer", "The trusted provider configuration is incomplete.");

        var cacheKey = $"agent-jwks:{provider.Issuer.AbsoluteUri}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<SecurityKey>? cached) && cached is not null)
            return cached;

        var client = httpClientFactory.CreateClient(nameof(AgentAssertionValidator));
        var json = await client.GetStringAsync(provider.JwksUri, cancellationToken);
        var keys = new JsonWebKeySet(json).GetSigningKeys().ToArray();
        if (keys.Length == 0)
            throw new AgentAssertionValidationException("invalid_signature", "The trusted provider published no signing keys.");

        cache.Set(cacheKey, keys, TimeSpan.FromHours(1));
        return keys;
    }

    private static AgentAssertionValidationException CreateValidationException(Exception? exception, string artifact) =>
        exception switch
        {
            SecurityTokenExpiredException => new("expired", $"The {artifact} has expired."),
            SecurityTokenInvalidLifetimeException => new("expired", $"The {artifact} lifetime is invalid."),
            SecurityTokenInvalidAudienceException => new("invalid_audience", $"The {artifact} audience is invalid."),
            SecurityTokenInvalidIssuerException => new("invalid_issuer", $"The {artifact} issuer is invalid."),
            SecurityTokenInvalidTypeException => new("invalid_request", $"The {artifact} type is invalid."),
            _ => new("invalid_signature", $"The {artifact} signature could not be validated."),
        };

    private bool ValidateLifetime(
        DateTime? notBefore,
        DateTime? expires,
        SecurityToken _,
        TokenValidationParameters parameters)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return expires is not null
            && expires > now - parameters.ClockSkew
            && (notBefore is null || notBefore <= now + parameters.ClockSkew);
    }
}
