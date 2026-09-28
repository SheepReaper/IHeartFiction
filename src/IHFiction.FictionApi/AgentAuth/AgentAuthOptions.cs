namespace IHFiction.FictionApi.AgentAuth;

internal sealed class AgentAuthOptions
{
    public const string SectionName = "AgentAuth";

    public Uri? Issuer { get; set; }
    public string Audience { get; set; } = "fiction-api";
    public string SigningKeyId { get; set; } = "agent-auth-1";
    public string? SigningKeyPem { get; set; }
    public Uri? ClaimVerificationUri { get; set; }
    public TimeSpan ProviderAssertionMaxAge { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan ServiceAssertionLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan ClaimLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public int ClaimPollIntervalSeconds { get; set; } = 5;
    public List<TrustedAgentProviderOptions> TrustedProviders { get; set; } = [];
    public List<AgentVerificationKeyOptions> PreviousVerificationKeys { get; set; } = [];
}

internal sealed class AgentVerificationKeyOptions
{
    public string KeyId { get; set; } = string.Empty;
    public string PublicKeyPem { get; set; } = string.Empty;
}

internal sealed class TrustedAgentProviderOptions
{
    public Uri? Issuer { get; set; }
    public Uri? JwksUri { get; set; }
    public List<string> ClientIds { get; set; } = [];
    public List<string> SigningAlgorithms { get; set; } = ["ES256", "RS256"];
}
