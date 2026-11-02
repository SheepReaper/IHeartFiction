using Microsoft.AspNetCore.Authorization;

namespace IHFiction.SharedWeb.Features.AccountManagement;

internal static class LogoutNavigation
{
    internal static string GetReturnUrl(Type pageType, string currentUrl, string baseUrl)
    {
        var attributes = pageType.GetCustomAttributes(inherit: true);
        var requiresAuthorization = attributes.OfType<IAuthorizeData>().Any()
            && !attributes.OfType<IAllowAnonymous>().Any();

        return requiresAuthorization
            ? new Uri(baseUrl).AbsolutePath
            : new Uri(currentUrl).PathAndQuery;
    }
}
