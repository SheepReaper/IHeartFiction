using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using FluentAssertions;

using IHFiction.FictionApi.Infrastructure;

namespace IHFiction.UnitTests.Infrastructure;

public class LinkServiceTests
{
    [Fact]
    public void Create_WhenHttpContextMissing_ThrowsArgumentNullException()
    {
        // Arrange
        var linkGenerator = new StubLinkGenerator("https://localhost/stories");
        var httpContextAccessor = new HttpContextAccessor();

        var sut = new LinkService(linkGenerator, httpContextAccessor);

        // Act
        var act = () => sut.Create("stories-list", "self");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WhenLinkGeneratorReturnsNull_ThrowsArgumentException()
    {
        // Arrange
        var linkGenerator = new StubLinkGenerator(null);
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

        var sut = new LinkService(linkGenerator, httpContextAccessor);

        // Act
        var act = () => sut.Create("stories-list", "self");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Link generation failed*uri generation parameters*");
    }

    [Fact]
    public void Create_WhenMethodNotProvided_DefaultsToGet()
    {
        // Arrange
        var linkGenerator = new StubLinkGenerator("https://localhost/stories/published");
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

        var sut = new LinkService(linkGenerator, httpContextAccessor);

        // Act
        var link = sut.Create("stories-list", "self");

        // Assert
        link.Href.Should().Be("https://localhost/stories/published");
        link.Rel.Should().Be("self");
        link.Method.Should().Be(HttpMethods.Get);
    }

    [Fact]
    public void Create_WhenMethodProvided_UsesProvidedMethod()
    {
        // Arrange
        var linkGenerator = new StubLinkGenerator("https://localhost/stories/123");
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

        var sut = new LinkService(linkGenerator, httpContextAccessor);

        // Act
        var link = sut.Create("stories-get", "self", HttpMethods.Post);

        // Assert
        link.Method.Should().Be(HttpMethods.Post);
    }

    [Fact]
    public void CreateGeneric_UsesEndpointNameFromType()
    {
        // Arrange
        var linkGenerator = new StubLinkGenerator("https://localhost/fake-endpoint");
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };

        var sut = new LinkService(linkGenerator, httpContextAccessor);

        // Act
        var link = sut.Create<FakeEndpoint>("self");

        // Assert
        link.Href.Should().Be("https://localhost/fake-endpoint");
        link.Rel.Should().Be("self");
        link.Method.Should().Be(HttpMethods.Get);
        linkGenerator.LastHttpContext.Should().Be(httpContext);
        linkGenerator.LastAddress.Should().NotBeNull();
    }

    [Fact]
    public void Create_WhenValuesProvided_ForwardsValuesToRouteDictionary()
    {
        // Arrange
        var linkGenerator = new StubLinkGenerator("https://localhost/stories/123");
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var sut = new LinkService(linkGenerator, httpContextAccessor);

        var values = new[]
        {
            new KeyValuePair<string, string?>("id", "123"),
            new KeyValuePair<string, string?>("sort", "title")
        };

        // Act
        _ = sut.Create("stories-get", "self", values: values);

        // Assert
        linkGenerator.LastValues.Should().NotBeNull();
        linkGenerator.LastValues!["id"].Should().Be("123");
        linkGenerator.LastValues!["sort"].Should().Be("title");
    }

    private sealed class StubLinkGenerator(string? uriToReturn) : LinkGenerator
    {
        public object? LastAddress { get; private set; }
        public RouteValueDictionary? LastValues { get; private set; }
        public HttpContext? LastHttpContext { get; private set; }

        public override string? GetPathByAddress<TAddress>(
            HttpContext httpContext,
            TAddress address,
            RouteValueDictionary values,
            RouteValueDictionary? ambientValues = null,
            PathString? pathBase = null,
            FragmentString fragment = default,
            LinkOptions? options = null)
        {
            LastHttpContext = httpContext;
            LastAddress = address;
            LastValues = values;

            return null;
        }

        public override string? GetUriByAddress<TAddress>(
            HttpContext httpContext,
            TAddress address,
            RouteValueDictionary values,
            RouteValueDictionary? ambientValues = null,
            string? scheme = null,
            HostString? host = null,
            PathString? pathBase = null,
            FragmentString fragment = default,
            LinkOptions? options = null)
        {
            LastHttpContext = httpContext;
            LastAddress = address;
            LastValues = values;

            return uriToReturn;
        }

        public override string? GetPathByAddress<TAddress>(
            TAddress address,
            RouteValueDictionary values,
            PathString pathBase = default,
            FragmentString fragment = default,
            LinkOptions? options = null)
        {
            throw new NotSupportedException("Path generation is not used by LinkService tests.");
        }

        public override string? GetUriByAddress<TAddress>(
            TAddress address,
            RouteValueDictionary values,
            string scheme,
            HostString host,
            PathString pathBase,
            FragmentString fragment = default,
            LinkOptions? options = null)
        {
            LastAddress = address;
            LastValues = values;

            return uriToReturn;
        }
    }

    private sealed class FakeEndpoint : INameEndpoint<FakeEndpoint>
    {
        public static string EndpointName => "fake-endpoint";
    }
}
