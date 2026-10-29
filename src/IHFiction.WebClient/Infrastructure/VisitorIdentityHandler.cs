using System.Net;

namespace IHFiction.WebClient.Infrastructure;

// Constructed in the caller scope, never in IHttpClientFactory's pooled handler scope.
internal sealed partial class VisitorIdentityHandler(VisitorContext visitor, ILogger<VisitorIdentityHandler> logger) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("X-Forwarded-For");
        if (IPAddress.TryParse(visitor.ClientIp, out var address))
            request.Headers.TryAddWithoutValidation("X-Forwarded-For", VisitorContext.Normalize(address));
        else
            MissingVisitorIdentity();
        return base.SendAsync(request, cancellationToken);
    }

    [LoggerMessage(EventId = 410, Level = LogLevel.Warning, Message = "FictionApi call has no validated visitor IP; using server identity")]
    private partial void MissingVisitorIdentity();
}
