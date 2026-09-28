using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics.CodeAnalysis;

using IHFiction.Data.AgentAuth.Domain;
using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Contexts;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using StackExchange.Redis;

namespace IHFiction.FictionApi.AgentAuth;

internal sealed record AgentRegistrationResult(
    Ulid RegistrationId,
    string? IdentityAssertion,
    DateTime? AssertionExpires,
    string? ClaimToken,
    string? UserCode,
    DateTime? ClaimExpires,
    int PollIntervalSeconds);

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "Standard exception constructors are required by exception design analyzers.")]
public sealed class AgentProtocolException : Exception
{
    public AgentProtocolException() { }
    public AgentProtocolException(string message) : base(message) { }
    public AgentProtocolException(string message, Exception innerException) : base(message, innerException) { }
    public AgentProtocolException(string error, string description, int statusCode = StatusCodes.Status400BadRequest) : base(description)
    {
        Error = error;
        StatusCode = statusCode;
    }
    public string Error { get; } = "server_error";
    public int StatusCode { get; } = StatusCodes.Status500InternalServerError;
}

internal sealed partial class AgentRegistrationService(
    FictionDbContext context,
    AgentAssertionValidator assertionValidator,
    AgentTokenService tokenService,
    KeycloakAdminService keycloak,
    UserService users,
    IConnectionMultiplexer redis,
    ILogger<AgentRegistrationService> logger,
    IOptions<AgentAuthOptions> options,
    TimeProvider timeProvider)
{
    private readonly AgentAuthOptions _options = options.Value;

    public async Task<AgentRegistrationResult> RegisterAsync(
        string assertionType,
        string assertion,
        CancellationToken cancellationToken)
    {
        ValidatedAgentIdentity identity;
        try
        {
            identity = await assertionValidator.ValidateAsync(assertionType, assertion, cancellationToken);
        }
        catch (AgentAssertionValidationException exception)
        {
            throw new AgentProtocolException(
                exception.Error,
                exception.Message,
                exception.Error is "login_required" ? StatusCodes.Status401Unauthorized : StatusCodes.Status400BadRequest);
        }

        if (await IsReplayCachedAsync(identity.Issuer, identity.AssertionJti))
            throw new AgentProtocolException("replay_detected", "The assertion has already been used.");

        if (await context.AgentAssertionReplays.AnyAsync(
            replay => replay.ProviderIssuer == identity.Issuer && replay.AssertionJti == identity.AssertionJti,
            cancellationToken))
            throw new AgentProtocolException("replay_detected", "The assertion has already been used.");

        var link = await context.AgentIdentityLinks.SingleOrDefaultAsync(
            candidate => candidate.ProviderIssuer == identity.Issuer
                && candidate.ProviderSubject == identity.Subject,
            cancellationToken);

        if (link is { RevokedAt: not null })
            throw new AgentProtocolException("invalid_grant", "This provider identity has been revoked.");

        if (link is not null)
            return await PersistRegistrationAsync(
                identity,
                () => CreateActiveRegistrationAsync(identity, link.UserId, link.Id, cancellationToken),
                cancellationToken);

        var keycloakUserResult = await keycloak.FindUserIdByEmailAsync(identity.VerifiedEmail, cancellationToken);
        if (keycloakUserResult.IsFailure)
            throw new AgentProtocolException("server_error", keycloakUserResult.DomainError.Description ?? "Keycloak user lookup failed.", StatusCodes.Status503ServiceUnavailable);

        if (keycloakUserResult.Value is { } existingUserId)
            return await PersistRegistrationAsync(
                identity,
                () => CreatePendingRegistrationAsync(identity, existingUserId, cancellationToken),
                cancellationToken);

        var createResult = await keycloak.CreateVerifiedUserAsync(identity.VerifiedEmail, cancellationToken);
        if (createResult.IsFailure)
            throw new AgentProtocolException("server_error", createResult.DomainError.Description ?? "Keycloak user creation failed.", StatusCodes.Status503ServiceUnavailable);

        var userId = createResult.Value;
        return await PersistRegistrationAsync(identity, async () =>
        {
            var localUserResult = await users.GetOrCreateUserAsync(
                userId,
                () => User.FromUserId(userId, identity.VerifiedEmail),
                cancellationToken);
            if (localUserResult.IsFailure)
                throw new AgentProtocolException("server_error", localUserResult.DomainError.Description ?? "Local user creation failed.", StatusCodes.Status500InternalServerError);

            var newLink = new AgentIdentityLink
            {
                ProviderIssuer = identity.Issuer,
                ProviderSubject = identity.Subject,
                VerifiedEmail = identity.VerifiedEmail,
                UserId = userId,
            };
            context.AgentIdentityLinks.Add(newLink);
            await context.SaveChangesAsync(cancellationToken);

            return await CreateActiveRegistrationAsync(identity, userId, newLink.Id, cancellationToken);
        }, cancellationToken);
    }

    public async Task ConfirmAsync(string userCode, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var userIdResult = principal.GetUid();
        if (userIdResult.IsFailure)
            throw new AgentProtocolException("invalid_user", userIdResult.DomainError.Description ?? "The user identifier is invalid.", StatusCodes.Status401Unauthorized);

        var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email)?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            throw new AgentProtocolException("invalid_user", "The signed-in account has no email claim.", StatusCodes.Status403Forbidden);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var codeHash = Hash(userCode.Replace("-", string.Empty, StringComparison.Ordinal).Trim());
        var registration = await context.AgentRegistrations.SingleOrDefaultAsync(
            candidate => candidate.UserCodeHash == codeHash,
            cancellationToken);

        if (registration is null || registration.ExpiresAt <= now)
            throw new AgentProtocolException("claim_expired", "The claim code is invalid or expired.");
        if (registration.Status != AgentRegistrationStatus.PendingClaim)
            throw new AgentProtocolException("claimed_or_in_flight", "The registration has already been claimed.");
        if (registration.UserId != userIdResult.Value
            || !string.Equals(registration.VerifiedEmail, email, StringComparison.OrdinalIgnoreCase))
            throw new AgentProtocolException("interaction_required", "Sign in with the account matching the verified assertion.", StatusCodes.Status403Forbidden);

        var existingLink = await context.AgentIdentityLinks.SingleOrDefaultAsync(
            candidate => candidate.ProviderIssuer == registration.ProviderIssuer
                && candidate.ProviderSubject == registration.ProviderSubject,
            cancellationToken);

        if (existingLink is null)
        {
            existingLink = new AgentIdentityLink
            {
                ProviderIssuer = registration.ProviderIssuer,
                ProviderSubject = registration.ProviderSubject,
                VerifiedEmail = registration.VerifiedEmail,
                UserId = userIdResult.Value,
            };
            context.AgentIdentityLinks.Add(existingLink);
        }
        else if (existingLink.UserId != userIdResult.Value || existingLink.RevokedAt is not null)
        {
            throw new AgentProtocolException(
                "interaction_required",
                "The provider identity is already linked to another account or has been revoked.",
                StatusCodes.Status403Forbidden);
        }

        registration.IdentityLinkId = existingLink.Id;
        registration.Status = AgentRegistrationStatus.Active;
        registration.ConfirmedAt = now;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(string AccessToken, int ExpiresIn, string Scope, string? IdentityAssertion, DateTime? AssertionExpires)> ExchangeAsync(
        string grantType,
        string? assertion,
        string? claimToken,
        string? resource,
        CancellationToken cancellationToken)
    {
        AgentRegistration registration;
        string? identityAssertion = null;
        DateTime? assertionExpires = null;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expectedResource = _options.Issuer?.AbsoluteUri.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(resource)
            || !string.Equals(resource.TrimEnd('/'), expectedResource, StringComparison.Ordinal))
            throw new AgentProtocolException("invalid_target", "resource must identify this API.");

        if (grantType == "urn:ietf:params:oauth:grant-type:jwt-bearer")
        {
            if (string.IsNullOrWhiteSpace(assertion))
                throw new AgentProtocolException("invalid_request", "assertion is required.");

            ClaimsPrincipal principal;
            try
            {
                var validation = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(
                    assertion,
                    tokenService.CreateServiceAssertionValidationParameters());
                if (!validation.IsValid || validation.ClaimsIdentity is null)
                    throw new Microsoft.IdentityModel.Tokens.SecurityTokenException();
                principal = new ClaimsPrincipal(validation.ClaimsIdentity);
            }
            catch (Microsoft.IdentityModel.Tokens.SecurityTokenException)
            {
                throw new AgentProtocolException("invalid_grant", "The identity assertion is invalid or expired.");
            }

            if (principal.FindFirstValue(AgentTokenService.TokenKindClaim) != AgentTokenService.ServiceAssertionKind
                || !Ulid.TryParse(principal.FindFirstValue(AgentTokenService.RegistrationIdClaim), out var registrationId)
                || !string.Equals(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), registrationId.ToString(), StringComparison.Ordinal))
                throw new AgentProtocolException("invalid_grant", "The token is not a service identity assertion.");

            registration = await context.AgentRegistrations.SingleOrDefaultAsync(x => x.Id == registrationId, cancellationToken)
                ?? throw new AgentProtocolException("invalid_grant", "The registration does not exist.");
        }
        else if (grantType == "urn:workos:agent-auth:grant-type:claim")
        {
            if (string.IsNullOrWhiteSpace(claimToken))
                throw new AgentProtocolException("invalid_request", "claim_token is required.");

            var claimHash = Hash(claimToken);
            registration = await context.AgentRegistrations.SingleOrDefaultAsync(x => x.ClaimTokenHash == claimHash, cancellationToken)
                ?? throw new AgentProtocolException("expired_token", "The claim token is invalid or expired.");

            if (registration.ExpiresAt <= now)
                throw new AgentProtocolException("expired_token", "The claim token has expired.");

            if (registration.LastPolledAt is { } lastPoll
                && now - lastPoll < TimeSpan.FromSeconds(_options.ClaimPollIntervalSeconds))
                throw new AgentProtocolException("slow_down", "Poll no faster than the advertised interval.");

            registration.LastPolledAt = now;
            await context.SaveChangesAsync(cancellationToken);

            if (registration.Status == AgentRegistrationStatus.PendingClaim)
                throw new AgentProtocolException("authorization_pending", "The user has not confirmed the registration.");

            identityAssertion = tokenService.CreateServiceAssertion(
                registration.Id,
                registration.UserId!.Value,
                now,
                out var issuedAssertionExpires);
            assertionExpires = issuedAssertionExpires;
        }
        else
        {
            throw new AgentProtocolException("unsupported_grant_type", "The grant_type is not supported.");
        }

        if (registration.Status != AgentRegistrationStatus.Active
            || registration.RevokedAt is not null
            || registration.ExpiresAt <= now
            || registration.UserId is null)
            throw new AgentProtocolException("invalid_grant", "The registration is inactive, expired, or revoked.");

        var accessToken = tokenService.CreateAccessToken(
            registration.Id,
            registration.UserId.Value,
            now,
            out var jti,
            out var expiresAt);
        context.AgentAccessTokens.Add(new AgentAccessToken
        {
            RegistrationId = registration.Id,
            TokenJti = jti,
            ExpiresAt = expiresAt,
        });
        await context.SaveChangesAsync(cancellationToken);

        return (accessToken, (int)(expiresAt - now).TotalSeconds, "agent.read profile.read", identityAssertion, assertionExpires);
    }

    public async Task RevokeTokenAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var validation = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(token, tokenService.CreateValidationParameters());
            if (!validation.IsValid || validation.ClaimsIdentity is null) return;
            var principal = new ClaimsPrincipal(validation.ClaimsIdentity);
            var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
            if (jti is null) return;
            var record = await context.AgentAccessTokens.SingleOrDefaultAsync(x => x.TokenJti == jti, cancellationToken);
            if (record is not null)
            {
                record.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Microsoft.IdentityModel.Tokens.SecurityTokenException)
        {
            // RFC 7009 requires invalid tokens to be treated as successfully revoked.
        }
    }

    public async Task RevokeRegistrationAsync(string securityEventToken, CancellationToken cancellationToken)
    {
        ValidatedAgentRevocation revocation;
        try
        {
            revocation = await assertionValidator.ValidateRevocationEventAsync(securityEventToken, cancellationToken);
        }
        catch (AgentAssertionValidationException exception)
        {
            throw new AgentProtocolException(exception.Error, exception.Message);
        }

        if (await context.AgentAssertionReplays.AnyAsync(
            x => x.ProviderIssuer == revocation.Issuer && x.AssertionJti == revocation.EventJti,
            cancellationToken))
            throw new AgentProtocolException("replay_detected", "The security event has already been processed.");

        if (await IsReplayCachedAsync(revocation.Issuer, revocation.EventJti))
            throw new AgentProtocolException("replay_detected", "The security event has already been processed.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        context.AgentAssertionReplays.Add(new AgentAssertionReplay
        {
            ProviderIssuer = revocation.Issuer,
            AssertionJti = revocation.EventJti,
            ExpiresAt = revocation.ExpiresAt == default ? now.AddDays(1) : revocation.ExpiresAt,
        });

        var link = await context.AgentIdentityLinks.SingleOrDefaultAsync(
            x => x.ProviderIssuer == revocation.Issuer && x.ProviderSubject == revocation.Subject,
            cancellationToken);
        if (link is not null)
        {
            link.RevokedAt = now;
            var registrations = await context.AgentRegistrations
                .Where(x => x.IdentityLinkId == link.Id && x.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var registration in registrations)
            {
                registration.Status = AgentRegistrationStatus.Revoked;
                registration.RevokedAt = now;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        await CacheReplayAsync(revocation.Issuer, revocation.EventJti, revocation.ExpiresAt);
    }

    private async Task<AgentRegistrationResult> CreateActiveRegistrationAsync(
        ValidatedAgentIdentity identity,
        Guid userId,
        Ulid linkId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var registration = new AgentRegistration
        {
            ProviderIssuer = identity.Issuer,
            ProviderSubject = identity.Subject,
            VerifiedEmail = identity.VerifiedEmail,
            ProviderClientId = identity.ClientId,
            UserId = userId,
            IdentityLinkId = linkId,
            Status = AgentRegistrationStatus.Active,
            ExpiresAt = now.AddDays(30),
        };
        context.AgentRegistrations.Add(registration);
        await context.SaveChangesAsync(cancellationToken);
        var assertion = tokenService.CreateServiceAssertion(registration.Id, userId, now, out var expiresAt);
        return new(registration.Id, assertion, expiresAt, null, null, null, _options.ClaimPollIntervalSeconds);
    }

    private async Task<AgentRegistrationResult> CreatePendingRegistrationAsync(
        ValidatedAgentIdentity identity,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var claimToken = $"clm_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24))}";
        var userCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        var expiresAt = now.Add(_options.ClaimLifetime);
        var registration = new AgentRegistration
        {
            ProviderIssuer = identity.Issuer,
            ProviderSubject = identity.Subject,
            VerifiedEmail = identity.VerifiedEmail,
            ProviderClientId = identity.ClientId,
            UserId = userId,
            Status = AgentRegistrationStatus.PendingClaim,
            ClaimTokenHash = Hash(claimToken),
            UserCodeHash = Hash(userCode),
            ExpiresAt = expiresAt,
        };
        context.AgentRegistrations.Add(registration);
        await context.SaveChangesAsync(cancellationToken);
        return new(registration.Id, null, null, claimToken, userCode, expiresAt, _options.ClaimPollIntervalSeconds);
    }

    private async Task<AgentRegistrationResult> PersistRegistrationAsync(
        ValidatedAgentIdentity identity,
        Func<Task<AgentRegistrationResult>> createRegistration,
        CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            context.AgentAssertionReplays.Add(new AgentAssertionReplay
            {
                ProviderIssuer = identity.Issuer,
                AssertionJti = identity.AssertionJti,
                ExpiresAt = identity.ExpiresAt,
            });

            var registration = await createRegistration();
            await transaction.CommitAsync(cancellationToken);
            return registration;
        });

        await CacheReplayAsync(identity.Issuer, identity.AssertionJti, identity.ExpiresAt);
        return result;
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private async Task<bool> IsReplayCachedAsync(string issuer, string jti)
    {
        try
        {
            return await redis.GetDatabase().KeyExistsAsync(ReplayKey(issuer, jti));
        }
        catch (RedisException exception)
        {
            ReplayCacheUnavailable(exception);
            return false;
        }
    }

    private async Task CacheReplayAsync(string issuer, string jti, DateTime expiresAt)
    {
        var ttl = expiresAt - timeProvider.GetUtcNow().UtcDateTime;
        if (ttl <= TimeSpan.Zero) return;

        try
        {
            await redis.GetDatabase().StringSetAsync(ReplayKey(issuer, jti), RedisValue.EmptyString, ttl);
        }
        catch (RedisException exception)
        {
            ReplayCacheUnavailable(exception);
        }
    }

    private static string ReplayKey(string issuer, string jti) => $"agent-auth:replay:{Hash($"{issuer}\n{jti}")}";

    [LoggerMessage(LogLevel.Warning, "The Redis agent assertion replay cache is unavailable; PostgreSQL replay protection remains active.")]
    private partial void ReplayCacheUnavailable(Exception exception);
}
