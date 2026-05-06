using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace IHFiction.SharedWeb.Infrastructure;

public sealed class CookieOidcRefresher(IOptionsMonitor<OpenIdConnectOptions> oidcOptionsMonitor)
{
    private readonly OpenIdConnectProtocolValidator _oidcTokenValidator = new()
    {
        // 1. Nonce was used during and then deleted at the end of the Authorization Code flow,
        // so it doesn't exist in any subsequent asynchronous validation context.
        // 2. Even if we had it, it would be expired.
        // 3. It's not needed to request a new auth token via refresh token.
        RequireNonce = false,
    };

    public async Task ValidateOrRefreshCookieAsync(CookieValidatePrincipalContext context, string oidcScheme, double? refreshThresholdSeconds = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var expiresAtToken = context.Properties.GetTokenValue("expires_at");

        if (!DateTimeOffset.TryParse(expiresAtToken, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt)) return;

        var oidcOptions = oidcOptionsMonitor.Get(oidcScheme);
        var now = oidcOptions.TimeProvider?.GetUtcNow() ?? throw new InvalidOperationException("No time provider configured in OIDC options.");

        // If the token is not about to expire skip this process
        if (now < expiresAt - TimeSpan.FromSeconds(refreshThresholdSeconds ?? 60)) return;

        var refreshed = await RefreshPrincipalAsync(context.Properties, oidcScheme, context.HttpContext.RequestAborted);

        if (refreshed is null)
        {
            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(context.Scheme.Name);

            return;
        }

        context.ShouldRenew = true;
        context.ReplacePrincipal(refreshed.Value.Principal);
        context.Properties.StoreTokens(refreshed.Value.Tokens);
    }

    public async Task<bool> TryRefreshAuthenticationAsync(HttpContext httpContext, string cookieScheme, string oidcScheme, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var authenticateResult = await httpContext.AuthenticateAsync(cookieScheme);

        if (!authenticateResult.Succeeded || authenticateResult.Properties is null)
        {
            return false;
        }

        var refreshed = await RefreshPrincipalAsync(authenticateResult.Properties, oidcScheme, cancellationToken);

        if (refreshed is null)
        {
            await httpContext.SignOutAsync(cookieScheme);
            return false;
        }

        authenticateResult.Properties.StoreTokens(refreshed.Value.Tokens);
        await httpContext.SignInAsync(cookieScheme, refreshed.Value.Principal, authenticateResult.Properties);
        return true;
    }

    private async Task<(ClaimsPrincipal Principal, IReadOnlyList<AuthenticationToken> Tokens)?> RefreshPrincipalAsync(
        AuthenticationProperties properties,
        string oidcScheme,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var oidcOptions = oidcOptionsMonitor.Get(oidcScheme);
        var now = oidcOptions.TimeProvider?.GetUtcNow() ?? throw new InvalidOperationException("No time provider configured in OIDC options.");

        var oidcConfiguration = await oidcOptions.ConfigurationManager!.GetConfigurationAsync(cancellationToken);
        var tokenEndpoint = oidcConfiguration.TokenEndpoint;

        using var body = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            [OpenIdConnectParameterNames.ClientId] = oidcOptions.ClientId,
            [OpenIdConnectParameterNames.ClientSecret] = oidcOptions.ClientSecret,
            [OpenIdConnectParameterNames.GrantType] = OpenIdConnectGrantTypes.RefreshToken,
            [OpenIdConnectParameterNames.RefreshToken] = properties.GetTokenValue(OpenIdConnectParameterNames.RefreshToken),
            [OpenIdConnectParameterNames.Scope] = string.Join(" ", oidcOptions.Scope),
        });

        using var response = await oidcOptions.Backchannel.PostAsync(new Uri(tokenEndpoint), body, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var token = new OpenIdConnectMessage(await response.Content.ReadAsStringAsync(cancellationToken));
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
            return null;
        }

        var validatedIdToken = JwtSecurityTokenConverter.Convert(validationResult.SecurityToken as JsonWebToken);

        validatedIdToken.Payload[OpenIdConnectParameterNames.Nonce] = null;
        _oidcTokenValidator.ValidateTokenResponse(new()
        {
            ProtocolMessage = token,
            ClientId = oidcOptions.ClientId,
            ValidatedIdToken = validatedIdToken
        });

        var tokens = new List<AuthenticationToken>
        {
            new(){ Name = OpenIdConnectParameterNames.AccessToken, Value = token.AccessToken },
            new(){ Name = OpenIdConnectParameterNames.IdToken, Value = token.IdToken },
            new(){ Name = OpenIdConnectParameterNames.RefreshToken, Value = token.RefreshToken },
            new(){ Name = OpenIdConnectParameterNames.TokenType, Value = token.TokenType },
            new(){ Name = "expires_at", Value = now.AddSeconds(double.Parse(token.ExpiresIn, NumberStyles.Float, CultureInfo.InvariantCulture))
                .ToString("o", CultureInfo.InvariantCulture) }
        };

        return (new ClaimsPrincipal(validationResult.ClaimsIdentity), tokens);
    }
}