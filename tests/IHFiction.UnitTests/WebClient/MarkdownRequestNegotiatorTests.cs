using FluentAssertions;

using IHFiction.WebClient.MarkdownResponses;

using Microsoft.AspNetCore.Http;

namespace IHFiction.UnitTests.WebClient;

public sealed class MarkdownRequestNegotiatorTests
{
    [Theory]
    [InlineData("text/markdown", true)]
    [InlineData("text/markdown; charset=utf-8", true)]
    [InlineData("text/html;q=0.5, text/markdown;q=0.9", true)]
    [InlineData("text/html, text/markdown;q=0.9", false)]
    [InlineData("text/html, text/markdown", false)]
    [InlineData("text/markdown;q=0.5, */*;q=1", false)]
    [InlineData("text/markdown;q=0", false)]
    [InlineData("*/*", false)]
    [InlineData("text/*", false)]
    [InlineData("not a media type", false)]
    public void PrefersMarkdown_UsesQualityAndSpecificity(string accept, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.Accept = accept;

        MarkdownRequestNegotiator.PrefersMarkdown(context.Request).Should().Be(expected);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public void PrefersMarkdown_ForUnsupportedMethod_ReturnsFalse(string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Headers.Accept = "text/markdown";

        MarkdownRequestNegotiator.PrefersMarkdown(context.Request).Should().BeFalse();
    }

}