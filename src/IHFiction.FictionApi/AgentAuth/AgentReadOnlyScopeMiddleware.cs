namespace IHFiction.FictionApi.AgentAuth;

internal sealed class AgentReadOnlyScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identities.Any(identity =>
                identity.IsAuthenticated
                && string.Equals(identity.AuthenticationType, AgentTokenService.AuthenticationScheme, StringComparison.Ordinal))
            && !HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(context);
    }
}
