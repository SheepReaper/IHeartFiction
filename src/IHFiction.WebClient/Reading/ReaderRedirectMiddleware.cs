using IHFiction.SharedWeb;
using IHFiction.SharedWeb.Services;

namespace IHFiction.WebClient.Reading;

internal sealed class ReaderRedirectMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IPublishedWorkReader reader)
    {
        if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            && context.Request.Path.StartsWithSegments("/read", out var remaining)
            && Ulid.TryParse(remaining.Value?.Trim('/'), out var id))
        {
            var result = await reader.GetPublishedWorkMetaAsync(id, cancellationToken: context.RequestAborted);
            if (result.IsSuccess)
            {
                if (ReaderRoutes.ContainerDestination(result.Value) is { } destination)
                {
                    context.Response.StatusCode = StatusCodes.Status303SeeOther;
                    context.Response.Headers.Location = destination;
                    return;
                }

                // The prerendered page can reuse this validated metadata.
                context.Items[typeof(LinkedOfGetPublishedWorkMetaResponse)] = result.Value;
            }
        }
        await next(context);
    }
}
