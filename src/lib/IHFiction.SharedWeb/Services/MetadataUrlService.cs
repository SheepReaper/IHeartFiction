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
                throw new InvalidOperationException("BaseUrl must be configured as an absolute URL.");
            }

            return baseUri;
        }
    }

    public string ToAbsolute(string pathOrUrl)
    {
        ArgumentNullException.ThrowIfNull(pathOrUrl);

        return ToAbsolute(new Uri(pathOrUrl.Trim(), UriKind.RelativeOrAbsolute));
    }

    public string ToAbsolute(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.IsAbsoluteUri)
        {
            if (!string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                return uri.ToString();
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
            : ToAbsolute(new Uri(pathOrUrl.Trim(), UriKind.RelativeOrAbsolute));

    public string? ToAbsoluteOrNull(Uri? uri) =>
        uri is null ? null : ToAbsolute(uri);
}
