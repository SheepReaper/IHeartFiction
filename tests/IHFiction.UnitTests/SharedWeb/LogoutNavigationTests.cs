using IHFiction.SharedWeb.Extensions;
using IHFiction.SharedWeb.Features.AccountManagement;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace IHFiction.UnitTests.SharedWeb;

public sealed class LogoutNavigationTests
{
    [Theory]
    [InlineData(typeof(PublicPage), "/app/stories?q=Ghost&sort=title")]
    [InlineData(typeof(ProtectedPage), "/app/")]
    [InlineData(typeof(InheritedProtectedPage), "/app/")]
    [InlineData(typeof(AnonymousPage), "/app/stories?q=Ghost&sort=title")]
    public void RouteDestination_UsesExistingInheritedAuthorizationMetadata(Type pageType, string expected)
    {
        Assert.Equal(expected, LogoutNavigation.GetReturnUrl(pageType,
            "https://example.test/app/stories?q=Ghost&sort=title", "https://example.test/app/"));
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/stories?q=Ghost&sort=title", "/stories?q=Ghost&sort=title")]
    [InlineData("https://evil.test/stories", "/")]
    [InlineData("//evil.test", "/")]
    [InlineData("/\\evil.test", "/")]
    [InlineData("javascript:alert(1)", "/")]
    [InlineData("stories", "/")]
    [InlineData("/stories\n", "/")]
    public void LogoutDestination_RejectsNonlocalValues(string? supplied, string expected)
        => Assert.Equal(expected, LoginLogoutEndpointRouteBuilderExtensions.CreateLogoutProperties(supplied, new DefaultHttpContext()).RedirectUri);

    [Theory]
    [InlineData(null, "/app/")]
    [InlineData("~/stories?q=Ghost", "/app/stories?q=Ghost")]
    public void LogoutDestination_RespectsPathBase(string? supplied, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = "/app";
        Assert.Equal(expected, LoginLogoutEndpointRouteBuilderExtensions.CreateLogoutProperties(supplied, context).RedirectUri);
    }

    private class PublicPage;
    [Authorize(Roles = "author")]
    private class ProtectedPage;
    private class InheritedProtectedPage : ProtectedPage;
    [AllowAnonymous]
    private class AnonymousPage : ProtectedPage;
}
