using FluentAssertions;

using IHFiction.WebClient.MarkdownResponses;

namespace IHFiction.UnitTests.WebClient;

public sealed class HtmlToMarkdownConverterTests
{
    private readonly HtmlToMarkdownConverter _sut = new();

    [Fact]
    public void Convert_ExtractsReadableContentAndRemovesRuntimeMarkup()
    {
        const string html = """
            <!doctype html>
            <html><body>
            <nav>Site navigation</nav>
            <main data-agent-content>
              <h1>Story &amp; Notes</h1>
              <p>A <strong>bold</strong> paragraph with <a href="/stories/1">a link</a>.</p>
              <ul><li>First</li><li>Second</li></ul>
              <img src="/cover.png" alt="Cover">
              <table><thead><tr><th>Name</th></tr></thead><tbody><tr><td>Value</td></tr></tbody></table>
              <script>window.bad = true;</script>
              <style>.bad { display: block; }</style>
              <div data-nosnippet>Runtime error UI</div>
              <!--Blazor:marker-->
            </main>
            <script src="_framework/blazor.web.js"></script>
            </body></html>
            """;

        var result = _sut.Convert(html);

        result.Should().Contain("# Story & Notes")
            .And.Contain("**bold**")
            .And.Contain("[a link](/stories/1)")
            .And.Contain("![Cover](/cover.png)")
            .And.Contain("| Name |")
            .And.NotContain("Site navigation")
            .And.NotContain("window.bad")
            .And.NotContain("Runtime error UI")
            .And.NotContain("Blazor:marker");
        result.Should().EndWith("\n");
    }

    [Fact]
    public void Convert_WithoutAgentContent_ReturnsNull()
    {
        _sut.Convert("<main><h1>Unmarked</h1></main>").Should().BeNull();
    }

    [Fact]
    public void Convert_WithEmptyAgentContent_ReturnsSingleNewline()
    {
        _sut.Convert("<main data-agent-content></main>").Should().Be("\n");
    }
}
