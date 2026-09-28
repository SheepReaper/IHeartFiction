using IHFiction.Data.AgentAuth.Domain;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IHFiction.Data.AgentAuth.Configurations;

internal sealed class AgentIdentityLinkConfiguration : IEntityTypeConfiguration<AgentIdentityLink>
{
    public void Configure(EntityTypeBuilder<AgentIdentityLink> builder)
    {
        builder.ToTable("agent_identity_links");
        builder.Property(x => x.ProviderIssuer).HasMaxLength(500);
        builder.Property(x => x.ProviderSubject).HasMaxLength(500);
        builder.Property(x => x.VerifiedEmail).HasMaxLength(320);
        builder.HasIndex(x => new { x.ProviderIssuer, x.ProviderSubject }).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

internal sealed class AgentRegistrationConfiguration : IEntityTypeConfiguration<AgentRegistration>
{
    public void Configure(EntityTypeBuilder<AgentRegistration> builder)
    {
        builder.ToTable("agent_registrations");
        builder.Property(x => x.ProviderIssuer).HasMaxLength(500);
        builder.Property(x => x.ProviderSubject).HasMaxLength(500);
        builder.Property(x => x.VerifiedEmail).HasMaxLength(320);
        builder.Property(x => x.ProviderClientId).HasMaxLength(500);
        builder.Property(x => x.ClaimTokenHash).HasMaxLength(64);
        builder.Property(x => x.UserCodeHash).HasMaxLength(64);
        builder.HasIndex(x => x.ClaimTokenHash).IsUnique();
        builder.HasIndex(x => x.UserCodeHash)
            .IsUnique()
            .HasFilter("user_code_hash IS NOT NULL AND status = 0");
        builder.HasIndex(x => x.IdentityLinkId);
    }
}

internal sealed class AgentAccessTokenConfiguration : IEntityTypeConfiguration<AgentAccessToken>
{
    public void Configure(EntityTypeBuilder<AgentAccessToken> builder)
    {
        builder.ToTable("agent_access_tokens");
        builder.Property(x => x.TokenJti).HasMaxLength(100);
        builder.HasIndex(x => x.TokenJti).IsUnique();
        builder.HasIndex(x => x.RegistrationId);
    }
}

internal sealed class AgentAssertionReplayConfiguration : IEntityTypeConfiguration<AgentAssertionReplay>
{
    public void Configure(EntityTypeBuilder<AgentAssertionReplay> builder)
    {
        builder.ToTable("agent_assertion_replays");
        builder.Property(x => x.ProviderIssuer).HasMaxLength(500);
        builder.Property(x => x.AssertionJti).HasMaxLength(200);
        builder.HasIndex(x => new { x.ProviderIssuer, x.AssertionJti }).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);
    }
}
