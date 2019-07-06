using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace FictionScraper.Shared
{
    public class StoryProvider
    {
        public StoryProvider()
        {
        }

        public StoryProvider(StoryProviderInfo info) : this()
        {
            BaseUri = info.BaseUri;
            Name = info.Name;
        }

        public string Name { get; set; }
        public HtmlWeb HtmlWebClient { get; set; } = new HtmlWeb();
        public Uri BaseUri { get; set; } = new Uri("none:///");
        public Uri UriFragment { get; set; } = new Uri("/", UriKind.Relative);
        public Uri RequestUri => new Uri(BaseUri, UriFragment);
        public int FetchRequestTimeoutMilliSeconds { get; set; } = 4500;
        public HttpStatusCode LastStatusCodeReceived => HtmlWebClient.StatusCode;

        private CancellationTokenSource InternalCts { get; } = new CancellationTokenSource();
        private CancellationToken InternalToken => InternalCts.Token;

        public async Task<HtmlDocument> LoadFromWebAsync()
        {
            return await ExecuteHtmlClientAsync(RequestUri, InternalToken);
        }

        public async Task<HtmlDocument> LoadFromWebAsync(CancellationToken ctx)
        {
            return await LoadFromWebAsync(RequestUri, ctx);
        }

        public async Task<HtmlDocument> LoadFromWebAsync(Uri uri)
        {
            return await ExecuteHtmlClientAsync(uri, InternalToken);
        }

        public async Task<HtmlDocument> LoadFromWebAsync(Uri uri, CancellationToken ctx)
        {
            using (var linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(InternalToken, ctx))
            {
                try
                {
                    return await ExecuteHtmlClientAsync(uri, linkedCts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    var newEx = InternalCts.IsCancellationRequested
                        ? new OperationCanceledException(
                            $"Operation Timed out. Time out value: {FetchRequestTimeoutMilliSeconds} seconds.", ex,
                            InternalToken)
                        : new OperationCanceledException("Operation cancelled by user.", ex, ctx);

                    newEx.Data["StoryProvider"] = this;

                    throw newEx;
                }
            }
        }

        private async Task<HtmlDocument> ExecuteHtmlClientAsync(Uri uri, CancellationToken ctx)
        {
            InternalCts.CancelAfter(FetchRequestTimeoutMilliSeconds);

            return await HtmlWebClient.LoadFromWebAsync(uri.AbsoluteUri, ctx);
        }
    }
}