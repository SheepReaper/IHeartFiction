using FluentAssertions;

using IHFiction.WebClient.Features.Reading;
namespace IHFiction.UnitTests.Features.Reading;

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
    public void Convert_RemovesFormChromeButKeepsReadableContent()
    {
        var result = _sut.Convert("""
            <main data-agent-content><h1>Stories</h1>
            <form><label>Search<input value="secret"></label><button>Submit</button></form>
            <label for="sort">Sort by</label><select id="sort"><option>Recently published</option></select>
            <textarea>Draft input</textarea><p>A story with <strong>real prose</strong>.</p></main>
            """);
        result.Should().Contain("# Stories").And.Contain("**real prose**")
            .And.NotContain("<form").And.NotContain("<select").And.NotContain("<textarea")
            .And.NotContain("Search").And.NotContain("Submit").And.NotContain("Recently published").And.NotContain("Draft input");
    }

    [Fact]
    public void Convert_SeparatesAdjacentBadgesWithoutChangingInlineProseOrCode()
    {
        var result = _sut.Convert("""
            <main data-agent-content><p><span class="tag">In progress</span><span class="tag">15 chapters</span><span class="tag">6 reads</span></p>
            <p>Un<span>break</span>able.</p><pre><code>&lt;span class="tag"&gt;literal&lt;/span&gt;</code></pre></main>
            """);
        result.Should().Contain("In progress 15 chapters 6 reads").And.Contain("Unbreakable.")
            .And.Contain("<span class=\"tag\">literal</span>");
    }

    [Fact]
    public void Convert_UsesAccessibleNamesForIconLinksAndPreservesImageLinks()
    {
        var result = _sut.Convert("""
            <main data-agent-content><a href="/read/1" aria-label="Read story"><span aria-hidden="true">glyph</span></a>
            <a href="/empty"><i class="fas fa-book"></i></a>
            <a href="/cover"><img src="/cover.png" alt="Cover art"></a>
            <a href="/text">Ordinary link</a></main>
            """);
        result.Should().Contain("[Read story](/read/1)").And.NotContain("glyph").And.NotContain("/empty")
            .And.Contain("![Cover art](/cover.png)").And.Contain("[Ordinary link](/text)");
    }

    [Fact]
    public void Convert_WithEmptyAgentContent_ReturnsSingleNewline()
    {
        _sut.Convert("<main data-agent-content></main>").Should().Be("\n");
    }
}
