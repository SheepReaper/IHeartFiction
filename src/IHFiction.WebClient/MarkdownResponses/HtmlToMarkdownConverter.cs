using AngleSharp.Html.Parser;

namespace IHFiction.WebClient.MarkdownResponses;

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

        foreach (var element in content.QuerySelectorAll("script, style, template, noscript, [data-nosnippet]"))
            element.Remove();

        return _converter.Convert(content.InnerHtml).Trim() + "\n";
    }
}