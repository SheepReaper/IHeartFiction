namespace IHFiction.FictionApi.Infrastructure;

/// <summary>
/// Names of the rate-limit policies registered by <c>AddApiRateLimiting</c>.
/// <see cref="PublicReads"/> is applied globally by default; override per endpoint or group with
/// <c>RequireRateLimiting(RateLimitPolicies.X)</c>. Endpoints mapped through <c>IEndpoint</c> that do not
/// choose a policy are assigned <see cref="Mutations"/> (non-GET/HEAD/OPTIONS) or
/// <see cref="AuthenticatedReads"/> (authorized reads) automatically.
/// </summary>
internal static class RateLimitPolicies
{
    /// <summary>Default for anonymous/public reads. Partitioned by client IP.</summary>
    public const string PublicReads = "public-reads";

    /// <summary>Authenticated reads. Partitioned by subject/client id, falling back to IP.</summary>
    public const string AuthenticatedReads = "authenticated-reads";

    /// <summary>Write operations (POST/PUT/PATCH/DELETE). Strict; partitioned by identity, falling back to IP.</summary>
    public const string Mutations = "mutations";

    /// <summary>Auth-sensitive endpoints (agent identity/token exchange). Strictest; partitioned by IP.</summary>
    public const string Auth = "auth";

    /// <summary>Qualified work-read recording (anonymous POST that is intentionally not treated as a mutation).</summary>
    public const string QualifiedReads = "qualified-reads";
}
