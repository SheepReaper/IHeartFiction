using IHFiction.SharedKernel.Searching;
using IHFiction.SharedWeb.Services;

namespace IHFiction.WebClient.Tags;

internal sealed class TagCanonicalizationMiddleware(RequestDelegate next)
{
    private const string Prefix = "/tags/";

    public async Task InvokeAsync(
        HttpContext context,
        TagService tagService,
        TagLandingRequestContext requestContext)
    {
        if ((!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            || !context.Request.Path.StartsWithSegments(Prefix.TrimEnd('/'), out var remaining)
            || !remaining.HasValue)
        {
            await next(context);
            return;
        }

        var requestedSpec = remaining.Value?.TrimStart('/');
        if (string.IsNullOrWhiteSpace(requestedSpec))
        {
            await next(context);
            return;
        }

        requestContext.RequestedSpec = requestedSpec;
        requestContext.Resolution = await tagService.ResolveAsync(requestedSpec, context.RequestAborted);

        if (requestContext.Resolution is { IsSuccess: true, Value: not null })
        {
            var canonicalPath = TagRouteSpec.BuildPath(requestContext.Resolution.Value.CanonicalSpec);
            if (!string.Equals(context.Request.Path.Value, canonicalPath, StringComparison.Ordinal))
            {
                context.Response.Redirect(canonicalPath + context.Request.QueryString, permanent: true);
                return;
            }
        }

        await next(context);
    }
}
