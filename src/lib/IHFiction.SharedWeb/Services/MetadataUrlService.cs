using Microsoft.Extensions.Options;

using IHFiction.SharedWeb.Configuration;

namespace IHFiction.SharedWeb.Services;

public sealed class MetadataUrlService(IOptions<SiteUrlOptions> siteUrlOptions)
{
    public Uri BaseUri
    {
        get
        {
            var baseUri = siteUrlOptions.Value.BaseUrl;

            if (baseUri is null)
            {
                throw new InvalidOperationException("BaseUrl must be configured as an absolute HTTP(S) URL.");
            }

            return baseUri;
        }
    }

    public string ToAbsolute(string pathOrUrl)
    {
        ArgumentNullException.ThrowIfNull(pathOrUrl);

        var normalizedPathOrUrl = pathOrUrl.Trim();

        if (normalizedPathOrUrl.Length == 0)
        {
            return BaseUri.ToString();
        }

        return ToAbsolute(new Uri(normalizedPathOrUrl, UriKind.RelativeOrAbsolute));
    }

    public string ToAbsolute(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.IsAbsoluteUri)
        {
            if (IsHttpScheme(uri))
            {
                return uri.ToString();
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Metadata URLs must use HTTP(S) or be relative paths.");
            }

            var candidate = string.IsNullOrWhiteSpace(uri.PathAndQuery)
                ? "/"
                : string.Concat(uri.PathAndQuery, uri.Fragment);

            return new Uri(BaseUri, candidate).ToString();
        }

        return new Uri(BaseUri, uri).ToString();
    }

    public string? ToAbsoluteOrNull(string? pathOrUrl) =>
        string.IsNullOrWhiteSpace(pathOrUrl)
            ? null
            : ToAbsoluteOrNull(new Uri(pathOrUrl.Trim(), UriKind.RelativeOrAbsolute));

    public string? ToAbsoluteOrNull(Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        if (uri.IsAbsoluteUri && !IsHttpScheme(uri) && !string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ToAbsolute(uri);
    }

    private static bool IsHttpScheme(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
