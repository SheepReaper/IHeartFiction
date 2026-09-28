using Microsoft.Extensions.Options;

namespace IHFiction.FictionApi.Infrastructure;

internal sealed class OAuthProtectedResourceChallengeMiddleware(RequestDelegate next)
{
    private const string ProtectedResourcePath = "/.well-known/oauth-protected-resource";
    private const string BearerScheme = "Bearer";
    private const string ResourceMetadataParameter = "resource_metadata";

    public async Task InvokeAsync(HttpContext context, IOptions<BaseUrlOptions> options)
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
            {
                AddProtectedResourceChallenge(context.Response, options.Value);
            }

            return Task.CompletedTask;
        });

        await next(context);
    }

    private static void AddProtectedResourceChallenge(HttpResponse response, BaseUrlOptions options)
    {
        var baseUrl = options.BaseUrl
            ?? throw new InvalidOperationException("The API BaseUrl must be configured.");
        var metadataUrl = new Uri(baseUrl, ProtectedResourcePath);
        var parameter = $"{ResourceMetadataParameter}=\"{metadataUrl.AbsoluteUri}\"";
        var challenges = response.Headers.WWWAuthenticate.ToArray();

        for (var index = 0; index < challenges.Length; index++)
        {
            var challenge = challenges[index];
            if (string.IsNullOrEmpty(challenge)) continue;
            if (!IsBearerChallenge(challenge)) continue;
            if (challenge.Contains(ResourceMetadataParameter, StringComparison.OrdinalIgnoreCase)) return;

            challenges[index] = challenge.Length == BearerScheme.Length
                ? $"{challenge} {parameter}"
                : $"{challenge}, {parameter}";
            response.Headers.WWWAuthenticate = challenges;
            return;
        }

        response.Headers.Append("WWW-Authenticate", $"{BearerScheme} {parameter}");
    }

    private static bool IsBearerChallenge(string challenge) =>
        challenge.StartsWith(BearerScheme, StringComparison.OrdinalIgnoreCase)
        && (challenge.Length == BearerScheme.Length || char.IsWhiteSpace(challenge[BearerScheme.Length]));
}
