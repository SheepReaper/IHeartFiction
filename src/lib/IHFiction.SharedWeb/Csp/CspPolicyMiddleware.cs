using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

using static IHFiction.SharedWeb.Csp.CspConstants;

namespace IHFiction.SharedWeb.Csp;

public sealed class CspPolicyMiddleware(
    RequestDelegate next,
    CspPolicyProvider policyProvider,
    IAuthenticationSchemeProvider authenticationSchemes,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var isRemoteSignOut = false;
        var schemes = await authenticationSchemes.GetAllSchemesAsync();
        foreach (var scheme in schemes.Where(s => s.HandlerType is not null && typeof(OpenIdConnectHandler).IsAssignableFrom(s.HandlerType)))
        {
            var remoteSignOutPath = oidcOptions.Get(scheme.Name).RemoteSignOutPath;
            if (remoteSignOutPath.HasValue && context.Request.Path == remoteSignOutPath)
            {
                isRemoteSignOut = true;
                break;
            }
        }

        context.Response.OnStarting(() =>
        {
            if (context.Items.TryGetValue(HttpContextItemKey, out var nonceObj) && nonceObj is string nonce)
            {
                context.Response.Headers["Reporting-Endpoints"] = policyProvider.ReportingEndpoints;
                context.Response.Headers["Report-To"] = policyProvider.ReportTo;

                // Enforced frame-ancestors takes precedence over legacy X-Frame-Options,
                // including SAMEORIGIN added by an upstream proxy (CSP section 6.4.2.2).
                if (isRemoteSignOut)
                {
                    context.Response.Headers.ContentSecurityPolicy = policyProvider.RenderRemoteSignOutPolicy(nonce);
                }
                else if (context.User.Identity?.IsAuthenticated == true &&
                    MediaTypeHeaderValue.TryParse(context.Response.ContentType, out var contentType) &&
                    contentType.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Headers.ContentSecurityPolicy = policyProvider.RenderAuthenticatedDocumentPolicy(nonce);
                }
                else
                {
                    context.Response.Headers.ContentSecurityPolicy = policyProvider.RenderPolicy(nonce);
                }

                var reportOnlyPolicy = policyProvider.RenderPolicyReportOnly(nonce);

                if (!string.IsNullOrWhiteSpace(reportOnlyPolicy))
                {
                    context.Response.Headers.ContentSecurityPolicyReportOnly = reportOnlyPolicy;
                }
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
