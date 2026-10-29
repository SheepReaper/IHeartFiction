using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace IHFiction.WebClient.Infrastructure;

// Own the scope-aware handlers and HttpClient, but leave pooled transport disposal to the factory.
internal sealed class FictionApiTransport : IDisposable
{
    internal const string Name = "FictionApiTransport";
    public HttpClient Client { get; }

    public FictionApiTransport(IHttpMessageHandlerFactory factory, IOptionsMonitor<HttpClientFactoryOptions> options,
        VisitorIdentityHandler visitor, AuthenticationHandler authentication)
    {
        authentication.InnerHandler = new BorrowedTransport(factory.CreateHandler(Name));
        visitor.InnerHandler = authentication;
        Client = new HttpClient(visitor);
        foreach (var configure in options.Get(Name).HttpClientActions)
            configure(Client);
    }

    public void Dispose() => Client.Dispose();

    private sealed class BorrowedTransport(HttpMessageHandler handler) : HttpMessageHandler
    {
        private readonly HttpMessageInvoker invoker = new(handler, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            invoker.SendAsync(request, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing) invoker.Dispose();
            base.Dispose(disposing);
        }
    }
}
