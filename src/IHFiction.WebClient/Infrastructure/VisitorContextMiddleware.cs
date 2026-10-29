namespace IHFiction.WebClient.Infrastructure;

internal sealed class VisitorContextMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, VisitorContext visitor)
    {
        visitor.ClientIp = VisitorContext.Normalize(context.Connection.RemoteIpAddress);
        return next(context);
    }
}
