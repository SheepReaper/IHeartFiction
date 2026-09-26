using Microsoft.Net.Http.Headers;

namespace IHFiction.WebClient.MarkdownResponses;

internal static class MarkdownRequestNegotiator
{
    private const string HtmlMediaType = "text/html";
    private const string MarkdownMediaType = "text/markdown";

    public static bool PrefersMarkdown(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!HttpMethods.IsGet(request.Method))
            return false;

        IList<MediaTypeHeaderValue> accepted;

        try
        {
            accepted = MediaTypeHeaderValue.ParseList(request.Headers.Accept);
        }
        catch (FormatException)
        {
            return false;
        }

        if (accepted.Count == 0 || !accepted.Any(IsExplicitMarkdown))
            return false;

        var markdownQuality = GetQuality(accepted, MarkdownMediaType);
        var htmlQuality = GetQuality(accepted, HtmlMediaType);

        return markdownQuality > 0 && markdownQuality > htmlQuality;
    }

    private static double GetQuality(IEnumerable<MediaTypeHeaderValue> accepted, string candidate)
    {
        var candidateParts = candidate.Split('/');
        var bestSpecificity = -1;
        var quality = 0d;

        foreach (var item in accepted)
        {
            var mediaType = item.MediaType.Value ?? string.Empty;
            var specificity = mediaType switch
            {
                var value when value.Equals(candidate, StringComparison.OrdinalIgnoreCase) => 2,
                var value when value.Equals($"{candidateParts[0]}/*", StringComparison.OrdinalIgnoreCase) => 1,
                "*/*" => 0,
                _ => -1
            };

            if (specificity > bestSpecificity)
            {
                bestSpecificity = specificity;
                quality = item.Quality ?? 1d;
            }
            else if (specificity >= 0 && specificity == bestSpecificity)
            {
                quality = Math.Max(quality, item.Quality ?? 1d);
            }
        }

        return quality;
    }

    private static bool IsExplicitMarkdown(MediaTypeHeaderValue value) =>
        string.Equals(value.MediaType.Value, MarkdownMediaType, StringComparison.OrdinalIgnoreCase)
        && (value.Quality ?? 1d) > 0;
}