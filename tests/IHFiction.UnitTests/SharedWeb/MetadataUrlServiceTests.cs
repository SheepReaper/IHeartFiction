using Microsoft.Extensions.Options;

using FluentAssertions;

using IHFiction.SharedWeb.Configuration;
using IHFiction.SharedWeb.Services;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class MetadataUrlServiceTests
{
    private static readonly Uri BaseUri = new("https://example.test/");

    [Fact]
    public void ToAbsolute_WithRelativePath_ReturnsAbsoluteUrl()
    {
        var sut = CreateSut();

        var result = sut.ToAbsolute("/images/cover.png");

        result.Should().Be("https://example.test/images/cover.png");
    }

    [Fact]
    public void ToAbsolute_WithWhitespace_ReturnsBaseUrl()
    {
        var sut = CreateSut();

        var result = sut.ToAbsolute("   ");

        result.Should().Be("https://example.test/");
    }

    [Fact]
    public void ToAbsolute_WithAbsoluteHttpUrl_PreservesInput()
    {
        var sut = CreateSut();

        var result = sut.ToAbsolute("https://cdn.example.test/cover.png");

        result.Should().Be("https://cdn.example.test/cover.png");
    }

    [Fact]
    public void ToAbsolute_WithFileUri_ResolvesAgainstBaseUrl()
    {
        var sut = CreateSut();

        var result = sut.ToAbsolute(new Uri("file:///images/cover.png?size=lg#v1"));

        result.Should().Be("https://example.test/images/cover.png?size=lg#v1");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("vbscript:msgbox(\"x\")")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    public void ToAbsolute_WithUnsupportedAbsoluteScheme_Throws(string input)
    {
        var sut = CreateSut();

        var act = () => sut.ToAbsolute(new Uri(input, UriKind.Absolute));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Metadata URLs must use HTTP(S) or be relative paths.");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("vbscript:msgbox(\"x\")")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    public void ToAbsoluteOrNull_WithUnsupportedAbsoluteScheme_ReturnsNull(string input)
    {
        var sut = CreateSut();

        var result = sut.ToAbsoluteOrNull(input);

        result.Should().BeNull();
    }

    [Fact]
    public void ToAbsoluteOrNull_WithUnsupportedUri_ReturnsNull()
    {
        var sut = CreateSut();

        var result = sut.ToAbsoluteOrNull(new Uri("data:text/plain;base64,SGVsbG8=", UriKind.Absolute));

        result.Should().BeNull();
    }

    private static MetadataUrlService CreateSut() =>
        new(Options.Create(new SiteUrlOptions { BaseUrl = BaseUri }));
}
