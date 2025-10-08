using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

using IHFiction.SharedWeb.Infrastructure;

namespace IHFiction.SharedWeb.Extensions;

public static class CookieExtensions
{
    public static IServiceCollection ConfigureCookieOidc(this IServiceCollection services, string cookieScheme, string oidcScheme, double? refreshThresholdSeconds = null)
    {
        services.AddSingleton<CookieOidcRefresher>();

        services.AddOptions<CookieAuthenticationOptions>(cookieScheme).Configure<CookieOidcRefresher>((cookieOptions, refresher) =>
        {
            cookieOptions.Cookie.Name = ".IHFiction.Auth";
            cookieOptions.Cookie.MaxAge = TimeSpan.FromDays(14);
            cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

            cookieOptions.SlidingExpiration = true;

            cookieOptions.Events.OnValidatePrincipal = context => refresher.ValidateOrRefreshCookieAsync(context, oidcScheme, refreshThresholdSeconds);
        });

        services.AddOptions<OpenIdConnectOptions>(oidcScheme).Configure(oidcOptions =>
        {
            // From Keycloak perspective, this requests a long refresh token, like a month, so that we may do things on behalf
            // of the user even when they're not logged in. Our app doesn't need to do anything while the user is no longer
            // signed in.
            // oidcOptions.Scope.Add(OpenIdConnectScope.OfflineAccess);

            oidcOptions.SaveTokens = true;

            oidcOptions.Events.OnRemoteFailure = async context =>
            {
                // If error_description would be "authentication_expired" and error would be "temporarily_unavailable",
                // then it's a pretty good guess that this is due to the user clicking the email verification link from
                // another browser window. In this case, navigate to /sign-in-again?reason=verified-from-external-window
                // else, if another reason can be determined, navigate to /sign-in-again?reason={reason}
                // else, if no reason can be determined, navigate to /sign-in-again (no reason query parameter)

                string? error = null;
                string? errorDescription = null;

                HttpRequest req = context.Request;

                // Ensure the request body can be read safely by us and by downstream components
                req.EnableBuffering();

                // Prefer form values when present (OIDC responses may be form-encoded)
                if (req.HasFormContentType)
                {
                    var form = await req.ReadFormAsync();
                    errorDescription = form["error_description"].FirstOrDefault();
                    error = form["error"].FirstOrDefault();

                    // Reset stream position so downstream middleware can also read the body
                    if (req.Body.CanSeek)
                        req.Body.Position = 0;
                }
                else
                {
                    // Read the body as a fallback. EnableBuffering so downstream readers aren't broken.
                    req.EnableBuffering();
                    using var reader = new StreamReader(req.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();
                    req.Body.Position = 0;

                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        // Try JSON first
                        if (req.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(body);
                                var root = doc.RootElement;
                                if (root.ValueKind == JsonValueKind.Object)
                                {
                                    if (root.TryGetProperty("error_description", out var ed) && ed.ValueKind == JsonValueKind.String)
                                        errorDescription = ed.GetString();
                                    if (root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                                        error = e.GetString();
                                }
                            }
                            catch (JsonException)
                            {
                                // ignore malformed JSON - proceed to form parsing
                            }
                        }

                        // If still not found, attempt to parse as form-encoded
                        if (errorDescription == null && error == null)
                        {
                            var parsed = QueryHelpers.ParseQuery(body);
                            if (parsed.TryGetValue("error_description", out var edv) && !string.IsNullOrWhiteSpace(edv))
                                errorDescription = edv.ToString();
                            if (parsed.TryGetValue("error", out var ev) && !string.IsNullOrWhiteSpace(ev))
                                error = ev.ToString();
                        }
                    }
                }

                string? reason = null;

                // Normalize and truncate the description before using it in a redirect
                const int maxReasonLength = 200;
                var normalizedDescription = errorDescription?.Trim();

                if (normalizedDescription?.Equals("authentication_expired", StringComparison.OrdinalIgnoreCase) == true
                    && error?.Equals("temporarily_unavailable", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // reason = "verified-from-external-window";
                    context.Response.Redirect("/");
                    context.HandleResponse();
                    return;
                }

                if (reason == null && !string.IsNullOrWhiteSpace(normalizedDescription))
                {
                    if (normalizedDescription.Length > maxReasonLength)
                        normalizedDescription = normalizedDescription[..maxReasonLength];

                    reason = normalizedDescription;
                }

                string? redirectUri = default;

                if (context.Properties is { } props)
                {
                    redirectUri = props.RedirectUri ?? (props.Items.TryGetValue(".redirect", out var ru) ? ru : null);
                }

                List<string> queryParams = [];

                if (!string.IsNullOrWhiteSpace(reason))
                    queryParams.Add("reason=" + WebUtility.UrlEncode(reason));

                if (!string.IsNullOrWhiteSpace(redirectUri))
                    queryParams.Add("returnUrl=" + WebUtility.UrlEncode(redirectUri));

                var query = queryParams.Count != 0 ? "?" + string.Join("&", queryParams) : string.Empty;

                context.Response.Redirect($"/sign-in-again{query}");
                context.HandleResponse();
            };
        });

        return services;
    }

    public static AuthenticationBuilder AddCookieWithOidcApiToken(this AuthenticationBuilder builder, string cookieScheme, string oidcSceme, double? refreshThresholdSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.ConfigureCookieOidc(cookieScheme, oidcSceme, refreshThresholdSeconds);

        builder.AddCookie(cookieScheme);

        return builder;
    }
}