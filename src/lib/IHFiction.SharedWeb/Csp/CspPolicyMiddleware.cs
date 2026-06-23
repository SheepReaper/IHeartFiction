using Microsoft.AspNetCore.Http;

using static IHFiction.SharedWeb.Csp.CspConstants;

namespace IHFiction.SharedWeb.Csp;

public sealed class CspPolicyMiddleware(RequestDelegate next, CspPolicyProvider policyProvider)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(() =>
        {
            if (context.Items.TryGetValue(HttpContextItemKey, out var nonceObj) && nonceObj is string nonce)
            {
                context.Response.Headers["Reporting-Endpoints"] = policyProvider.ReportingEndpoints;
                context.Response.Headers["Report-To"] = policyProvider.ReportTo;

                context.Response.Headers.ContentSecurityPolicy = policyProvider.RenderPolicy(nonce);

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
