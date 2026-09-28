using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.AgentAuth.Domain;

public sealed class AgentIdentityLink : DomainUlidEntityWithTimestamp
{
    public required string ProviderIssuer { get; set; }
    public required string ProviderSubject { get; set; }
    public required string VerifiedEmail { get; set; }
    public Guid UserId { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public enum AgentRegistrationStatus
{
    PendingClaim,
    Active,
    Revoked,
}

public sealed class AgentRegistration : DomainUlidEntityWithTimestamp
{
    public required string ProviderIssuer { get; set; }
    public required string ProviderSubject { get; set; }
    public required string VerifiedEmail { get; set; }
    public required string ProviderClientId { get; set; }
    public AgentRegistrationStatus Status { get; set; }
    public Guid? UserId { get; set; }
    public Ulid? IdentityLinkId { get; set; }
    public string? ClaimTokenHash { get; set; }
    public string? UserCodeHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class AgentAccessToken : DomainUlidEntityWithTimestamp
{
    public Ulid RegistrationId { get; set; }
    public required string TokenJti { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class AgentAssertionReplay : DomainUlidEntityWithTimestamp
{
    public required string ProviderIssuer { get; set; }
    public required string AssertionJti { get; set; }
    public DateTime ExpiresAt { get; set; }
}
