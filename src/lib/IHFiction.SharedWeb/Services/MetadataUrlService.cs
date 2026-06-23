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

    public string ToAbsolute(string pathOrUrl, bool preserveQueryParameters = false)
    {
        ArgumentNullException.ThrowIfNull(pathOrUrl);

        var normalizedPathOrUrl = pathOrUrl.Trim();

        if (normalizedPathOrUrl.Length == 0)
        {
            return BaseUri.ToString();
        }

        return ToAbsolute(new Uri(normalizedPathOrUrl, UriKind.RelativeOrAbsolute), preserveQueryParameters);
    }

    public string ToAbsolute(Uri uri, bool preserveQueryParameters = false)
    {
        ArgumentNullException.ThrowIfNull(uri);

        Uri absoluteUri;
        if (uri.IsAbsoluteUri)
        {
            if (IsHttpScheme(uri))
            {
                absoluteUri = uri;
                return preserveQueryParameters ? absoluteUri.ToString() : RemoveQueryParameters(absoluteUri);
            }

            if (!IsFileScheme(uri))
            {
                throw new InvalidOperationException("Metadata URLs must use HTTP(S) or be relative paths.");
            }

            var path = preserveQueryParameters ? uri.PathAndQuery : uri.AbsolutePath;
            var candidate = string.IsNullOrWhiteSpace(path)
                ? "/"
                : string.Concat(path, uri.Fragment);

            absoluteUri = new Uri(BaseUri, candidate);
            return preserveQueryParameters ? absoluteUri.ToString() : RemoveQueryParameters(absoluteUri);
        }

        absoluteUri = new Uri(BaseUri, uri);
        return preserveQueryParameters ? absoluteUri.ToString() : RemoveQueryParameters(absoluteUri);
    }

    public string? ToAbsoluteOrNull(string? pathOrUrl, bool preserveQueryParameters = false) =>
        string.IsNullOrWhiteSpace(pathOrUrl)
            ? null
            : ToAbsoluteOrNull(new Uri(pathOrUrl.Trim(), UriKind.RelativeOrAbsolute), preserveQueryParameters);

    public string? ToAbsoluteOrNull(Uri? uri, bool preserveQueryParameters = false)
    {
        if (uri is null)
        {
            return null;
        }

        if (uri.IsAbsoluteUri && !IsSupportedAbsoluteScheme(uri))
        {
            return null;
        }

        return ToAbsolute(uri, preserveQueryParameters);
    }

    private static string RemoveQueryParameters(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Query = string.Empty
        };

        return builder.Uri.ToString();
    }

    private static bool IsSupportedAbsoluteScheme(Uri uri) =>
        IsHttpScheme(uri) || IsFileScheme(uri);

    private static bool IsHttpScheme(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static bool IsFileScheme(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase);
}
