using System.Text;

using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.Net.Http.Headers;

namespace IHFiction.WebClient.MarkdownResponses;

internal sealed class MarkdownResponseMiddleware(RequestDelegate next)
{
    private const string ContentSignal = "ai-train=no, search=yes, ai-input=yes";

    public async Task InvokeAsync(HttpContext context, HtmlToMarkdownConverter converter)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!MarkdownRequestNegotiator.PrefersMarkdown(context.Request)
            || context.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>() is null)
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var bufferedBody = new MemoryStream();
        context.Response.Body = bufferedBody;

        try
        {
            await next(context);

            bufferedBody.Position = 0;

            if (context.Response.StatusCode != StatusCodes.Status200OK
                || !IsHtml(context.Response.ContentType))
            {
                await bufferedBody.CopyToAsync(originalBody, context.RequestAborted);
                return;
            }

            using var reader = new StreamReader(bufferedBody, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var html = await reader.ReadToEndAsync(context.RequestAborted);
            var markdown = converter.Convert(html);

            if (markdown is null)
            {
                bufferedBody.Position = 0;
                await bufferedBody.CopyToAsync(originalBody, context.RequestAborted);
                return;
            }

            context.Response.ContentType = "text/markdown; charset=utf-8";
            context.Response.ContentLength = null;
            context.Response.Headers["Content-Signal"] = ContentSignal;
            MergeVary(context.Response.Headers);

            await originalBody.WriteAsync(Encoding.UTF8.GetBytes(markdown), context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static bool IsHtml(string? contentType) =>
        MediaTypeHeaderValue.TryParse(contentType, out var parsed)
        && string.Equals(parsed.MediaType.Value, "text/html", StringComparison.OrdinalIgnoreCase);

    private static void MergeVary(IHeaderDictionary headers)
    {
        var values = headers.Vary
            .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
            .ToList();

        if (!values.Any(value => value.Equals(HeaderNames.Accept, StringComparison.OrdinalIgnoreCase)))
            values.Add(HeaderNames.Accept);

        headers.Vary = string.Join(", ", values);
    }
}