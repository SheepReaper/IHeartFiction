using IHFiction.SharedKernel.Searching;
using IHFiction.SharedWeb.Services;
using IHFiction.WebClient.MarkdownResponses;
using System.Net;

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

        if (requestContext.Resolution is { IsFailure: true } failure)
        {
            context.Response.StatusCode = failure.DomainError.Code.Split('.')[^1] switch
            {
                "NotFound" => StatusCodes.Status404NotFound,
                "Conflict" => StatusCodes.Status409Conflict,
                "InvalidSpec" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status502BadGateway
            };
            var markdown = MarkdownRequestNegotiator.PrefersMarkdown(context.Request);
            context.Response.ContentType = markdown ? "text/markdown; charset=utf-8" : "text/html; charset=utf-8";
            context.Response.Headers.Vary = "Accept";
            context.Response.Headers["X-Robots-Tag"] = "noindex";
            if (!HttpMethods.IsHead(context.Request.Method))
            {
                var description = failure.DomainError.Description ?? "Unable to resolve the requested tags.";
                await context.Response.WriteAsync(markdown
                    ? $"# Tag page unavailable\n\n{description}\n"
                    : $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\"><title>Tag page unavailable</title></head><body><main><h1>Tag page unavailable</h1><p>{WebUtility.HtmlEncode(description)}</p><a href=\"/tags\">Browse tags</a></main></body></html>", context.RequestAborted);
            }
            return;
        }

        await next(context);
    }
}
