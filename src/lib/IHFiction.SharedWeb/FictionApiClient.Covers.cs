using System.Globalization;

namespace IHFiction.SharedWeb;

public partial class FictionApiClient
{
    public async Task<HttpResponseMessage> GetStoryCoverResponseAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildStoryCoverUri(id));

        var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        return response;
    }

    private Uri BuildStoryCoverUri(string id)
    {
        var urlBuilder = new System.Text.StringBuilder();
        urlBuilder.Append("stories/");
        urlBuilder.Append(Uri.EscapeDataString(ConvertToString(id, CultureInfo.InvariantCulture)));
        urlBuilder.Append("/cover");

        return new Uri(urlBuilder.ToString(), UriKind.RelativeOrAbsolute);
    }
}