using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace IHFiction.SharedWeb.Infrastructure;

public sealed partial class CookieOidcRefresher(
    IOptionsMonitor<OpenIdConnectOptions> oidcOptionsMonitor,
    ILogger<CookieOidcRefresher> logger)
{
    private readonly OpenIdConnectProtocolValidator _oidcTokenValidator = new()
    {
        RequireNonce = false,
    };

    public async Task ValidateOrRefreshCookieAsync(CookieValidatePrincipalContext context, string oidcScheme, double? refreshThresholdSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var expiresAtToken = context.Properties.GetTokenValue("expires_at");
        if (!DateTimeOffset.TryParse(expiresAtToken, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt))
            return;

        var oidcOptions = oidcOptionsMonitor.Get(oidcScheme);
        var now = oidcOptions.TimeProvider?.GetUtcNow()
            ?? throw new InvalidOperationException("No time provider configured in OIDC options.");

        if (now < expiresAt - TimeSpan.FromSeconds(refreshThresholdSeconds ?? 60))
            return;

        if (context.Principal is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }

        var refreshed = await RefreshPrincipalAsync(context.Properties, context.Principal, oidcScheme, context.HttpContext.RequestAborted);

        if (refreshed.Status == RefreshStatus.InvalidSession)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }

        if (refreshed.Status == RefreshStatus.TransientFailure)
            return;

        context.ShouldRenew = true;
        context.ReplacePrincipal(refreshed.Principal!);
        context.Properties.StoreTokens(refreshed.Tokens!);
    }

    public async Task<bool> TryRefreshAuthenticationAsync(HttpContext httpContext, string cookieScheme, string oidcScheme, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var authenticateResult = await httpContext.AuthenticateAsync(cookieScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Properties is null || authenticateResult.Principal is null)
            return false;

        var refreshed = await RefreshPrincipalAsync(authenticateResult.Properties, authenticateResult.Principal, oidcScheme, cancellationToken);

        if (refreshed.Status == RefreshStatus.InvalidSession)
        {
            await httpContext.SignOutAsync(cookieScheme);
            return false;
        }

        if (refreshed.Status == RefreshStatus.TransientFailure)
            return false;

        authenticateResult.Properties.StoreTokens(refreshed.Tokens!);
        await httpContext.SignInAsync(cookieScheme, refreshed.Principal!, authenticateResult.Properties);
        return true;
    }

    internal async Task<RefreshResult> RefreshPrincipalAsync(
        AuthenticationProperties properties,
        ClaimsPrincipal currentPrincipal,
        string oidcScheme,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var refreshToken = properties.GetTokenValue(OpenIdConnectParameterNames.RefreshToken);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            LogMissingRefreshToken();
            return RefreshResult.InvalidSession();
        }

        var oidcOptions = oidcOptionsMonitor.Get(oidcScheme);
        var now = oidcOptions.TimeProvider?.GetUtcNow()
            ?? throw new InvalidOperationException("No time provider configured in OIDC options.");

        try
        {
            var configurationManager = oidcOptions.ConfigurationManager
                ?? throw new InvalidOperationException("No configuration manager configured in OIDC options.");
            var oidcConfiguration = await configurationManager.GetConfigurationAsync(cancellationToken);

            using var body = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                [OpenIdConnectParameterNames.ClientId] = oidcOptions.ClientId,
                [OpenIdConnectParameterNames.ClientSecret] = oidcOptions.ClientSecret,
                [OpenIdConnectParameterNames.GrantType] = OpenIdConnectGrantTypes.RefreshToken,
                [OpenIdConnectParameterNames.RefreshToken] = refreshToken,
                [OpenIdConnectParameterNames.Scope] = string.Join(" ", oidcOptions.Scope),
            });

            using var response = await oidcOptions.Backchannel.PostAsync(new Uri(oidcConfiguration.TokenEndpoint), body, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var token = new OpenIdConnectMessage(responseBody);

            if (!response.IsSuccessStatusCode)
            {
                if (IsInvalidSession(token.Error))
                {
                    LogRefreshTokenRejected(response.StatusCode);
                    return RefreshResult.InvalidSession();
                }

                LogRefreshProviderFailure(response.StatusCode, NormalizeError(token.Error));
                return RefreshResult.TransientFailure();
            }

            var principal = currentPrincipal;
            if (!string.IsNullOrWhiteSpace(token.IdToken))
            {
                var validationParameters = oidcOptions.TokenValidationParameters.Clone();

                if (oidcOptions.ConfigurationManager is BaseConfigurationManager baseConfigurationManager)
                {
                    validationParameters.ConfigurationManager = baseConfigurationManager;
                }
                else
                {
                    validationParameters.ValidIssuer = oidcConfiguration.Issuer;
                    validationParameters.IssuerSigningKeys = oidcConfiguration.SigningKeys;
                }

                var validationResult = await oidcOptions.TokenHandler.ValidateTokenAsync(token.IdToken, validationParameters);
                if (!validationResult.IsValid)
                {
                    LogInvalidIdToken();
                    return RefreshResult.InvalidSession();
                }

                var validatedIdToken = JwtSecurityTokenConverter.Convert(validationResult.SecurityToken as JsonWebToken);
                validatedIdToken.Payload[OpenIdConnectParameterNames.Nonce] = null;
                _oidcTokenValidator.ValidateTokenResponse(new()
                {
                    ProtocolMessage = token,
                    ClientId = oidcOptions.ClientId,
                    ValidatedIdToken = validatedIdToken,
                });

                principal = new ClaimsPrincipal(validationResult.ClaimsIdentity);
            }

            IReadOnlyList<AuthenticationToken> tokens;
            try
            {
                tokens = MergeRefreshedTokens(properties, token, now);
            }
            catch (FormatException exception)
            {
                LogMalformedRefreshResponse(exception);
                return RefreshResult.TransientFailure();
            }

            return RefreshResult.Success(principal, tokens);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogRefreshRequestTimedOut();
            return RefreshResult.TransientFailure();
        }
        catch (HttpRequestException exception)
        {
            LogRefreshRequestFailed(exception);
            return RefreshResult.TransientFailure();
        }
        catch (OpenIdConnectProtocolException exception)
        {
            LogMalformedRefreshResponse(exception);
            return RefreshResult.TransientFailure();
        }
    }

    internal static IReadOnlyList<AuthenticationToken> MergeRefreshedTokens(
        AuthenticationProperties properties,
        OpenIdConnectMessage token,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new FormatException("The refresh response did not contain an access token.");

        if (!double.TryParse(token.ExpiresIn, NumberStyles.Float, CultureInfo.InvariantCulture, out var expiresIn)
            || !double.IsFinite(expiresIn)
            || expiresIn <= 0)
            throw new FormatException("The refresh response did not contain a valid expiration.");

        var tokens = properties.GetTokens().ToDictionary(item => item.Name, StringComparer.Ordinal);

        SetToken(tokens, OpenIdConnectParameterNames.AccessToken, token.AccessToken);
        SetToken(tokens, OpenIdConnectParameterNames.IdToken, token.IdToken);
        SetToken(tokens, OpenIdConnectParameterNames.RefreshToken, token.RefreshToken);
        SetToken(tokens, OpenIdConnectParameterNames.TokenType, token.TokenType);
        SetToken(tokens, "expires_at", now.AddSeconds(expiresIn).ToString("o", CultureInfo.InvariantCulture));

        return [.. tokens.Values];
    }

    internal static bool IsInvalidSession(string? error) =>
        string.Equals(error, "invalid_grant", StringComparison.Ordinal);

    private static string? NormalizeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return null;

        const int maxLength = 64;
        var normalized = new string([.. error.Where(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.')]);
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static void SetToken(Dictionary<string, AuthenticationToken> tokens, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            tokens[name] = new AuthenticationToken { Name = name, Value = value };
    }

    [LoggerMessage(1, LogLevel.Warning, "The OIDC refresh token is missing; the authentication session is no longer refreshable.")]
    private partial void LogMissingRefreshToken();

    [LoggerMessage(2, LogLevel.Information, "The OIDC provider rejected the refresh token with HTTP {StatusCode}; the authentication session will be cleared.")]
    private partial void LogRefreshTokenRejected(HttpStatusCode statusCode);

    [LoggerMessage(3, LogLevel.Warning, "The OIDC provider returned HTTP {StatusCode} with error {Error} during token refresh; the authentication cookie was retained.")]
    private partial void LogRefreshProviderFailure(HttpStatusCode statusCode, string? error);

    [LoggerMessage(4, LogLevel.Warning, "The OIDC provider returned an invalid ID token during token refresh; the authentication session will be cleared.")]
    private partial void LogInvalidIdToken();

    [LoggerMessage(5, LogLevel.Warning, "The OIDC provider returned a malformed token refresh response; the authentication cookie was retained.")]
    private partial void LogMalformedRefreshResponse(Exception exception);

    [LoggerMessage(6, LogLevel.Warning, "The OIDC token refresh request timed out; the authentication cookie was retained.")]
    private partial void LogRefreshRequestTimedOut();

    [LoggerMessage(7, LogLevel.Warning, "The OIDC token refresh request failed; the authentication cookie was retained.")]
    private partial void LogRefreshRequestFailed(Exception exception);

    internal enum RefreshStatus
    {
        Success,
        InvalidSession,
        TransientFailure,
    }

    internal sealed record RefreshResult(
        RefreshStatus Status,
        ClaimsPrincipal? Principal = null,
        IReadOnlyList<AuthenticationToken>? Tokens = null)
    {
        public static RefreshResult Success(ClaimsPrincipal principal, IReadOnlyList<AuthenticationToken> tokens) =>
            new(RefreshStatus.Success, principal, tokens);

        public static RefreshResult InvalidSession() => new(RefreshStatus.InvalidSession);

        public static RefreshResult TransientFailure() => new(RefreshStatus.TransientFailure);
    }
}
