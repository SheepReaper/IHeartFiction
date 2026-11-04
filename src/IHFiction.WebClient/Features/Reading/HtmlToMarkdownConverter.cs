using AngleSharp.Html.Parser;

namespace IHFiction.WebClient.Features.Reading;

internal sealed class HtmlToMarkdownConverter
{
    private const string AgentContentSelector = "[data-agent-content]";

    private readonly ReverseMarkdown.Converter _converter = CreateConverter();

    private static ReverseMarkdown.Converter CreateConverter()
    {
        var config = new ReverseMarkdown.Config();
        config.Formatting.RemoveComments = true;
        config.Links.SmartHref = true;

        return new ReverseMarkdown.Converter(config);
    }

    public string? Convert(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var document = new HtmlParser().ParseDocument(html);
        var content = document.QuerySelector(AgentContentSelector);

        if (content is null)
            return null;

        foreach (var element in content.QuerySelectorAll("script, style, template, noscript, [data-nosnippet], form, input, select, textarea, button, label, fieldset, [aria-hidden='true']"))
            element.Remove();

        foreach (var badge in content.QuerySelectorAll(".tag").Where(badge => badge.Closest("pre, code") is null))
            badge.ParentElement?.InsertBefore(document.CreateTextNode(" "), badge.NextSibling);

        foreach (var link in content.QuerySelectorAll("a"))
        {
            if (!string.IsNullOrWhiteSpace(link.TextContent)
                || link.QuerySelectorAll("img").Any(image => !string.IsNullOrWhiteSpace(image.GetAttribute("alt"))))
                continue;

            var accessibleName = link.GetAttribute("aria-label");
            if (string.IsNullOrWhiteSpace(accessibleName))
                link.Remove();
            else
                link.TextContent = accessibleName;
        }

        return _converter.Convert(content.InnerHtml).Trim() + "\n";
    }
}
