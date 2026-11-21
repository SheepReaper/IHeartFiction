using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IHFiction.WebClient.Infrastructure;

internal sealed class FictionApiHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public const string Name = "fiction-api";
    public const string ClientName = "FictionApiHealth";

    private static readonly Uri HealthEndpointUri = new("/health", UriKind.Relative);

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Health check must return an Unhealthy result rather than throwing an unhandled exception.")]
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(ClientName);
            using var response = await client.GetAsync(HealthEndpointUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Fiction API is healthy.");
            }

            return HealthCheckResult.Unhealthy($"Fiction API returned status {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Fiction API is unreachable.", ex);
        }
    }
}
