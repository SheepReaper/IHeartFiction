using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
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

        var oidcConfiguration = await oidcOptions.ConfigurationManager!.GetConfigurationAsync(context.HttpContext.RequestAborted);
        var tokenEndpoint = oidcConfiguration.TokenEndpoint;

        using var body = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            [OpenIdConnectParameterNames.ClientId] = oidcOptions.ClientId,
            [OpenIdConnectParameterNames.ClientSecret] = oidcOptions.ClientSecret,
            [OpenIdConnectParameterNames.GrantType] = OpenIdConnectGrantTypes.RefreshToken,
            [OpenIdConnectParameterNames.RefreshToken] = context.Properties.GetTokenValue(OpenIdConnectParameterNames.RefreshToken),
            [OpenIdConnectParameterNames.Scope] = string.Join(" ", oidcOptions.Scope),
        });

        using var response = await oidcOptions.Backchannel.PostAsync(new Uri(tokenEndpoint), body);

        if (!response.IsSuccessStatusCode)
        {
            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(context.Scheme.Name);

            return;
        }

        var token = new OpenIdConnectMessage(await response.Content.ReadAsStringAsync());
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
            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(context.Scheme.Name);

            return;
        }

        var validatedIdToken = JwtSecurityTokenConverter.Convert(validationResult.SecurityToken as JsonWebToken);

        validatedIdToken.Payload[OpenIdConnectParameterNames.Nonce] = null;
        _oidcTokenValidator.ValidateTokenResponse(new()
        {
            ProtocolMessage = token,
            ClientId = oidcOptions.ClientId,
            ValidatedIdToken = validatedIdToken
        });

        context.ShouldRenew = true;
        context.ReplacePrincipal(new ClaimsPrincipal(validationResult.ClaimsIdentity));

        context.Properties.StoreTokens([
            new(){ Name = OpenIdConnectParameterNames.AccessToken, Value = token.AccessToken },
            new(){ Name = OpenIdConnectParameterNames.IdToken, Value = token.IdToken },
            new(){ Name = OpenIdConnectParameterNames.RefreshToken, Value = token.RefreshToken },
            new(){ Name = OpenIdConnectParameterNames.TokenType, Value = token.TokenType },
            new(){ Name = "expires_at", Value = now.AddSeconds(double.Parse(token.ExpiresIn, NumberStyles.Float, CultureInfo.InvariantCulture))
                .ToString("o", CultureInfo.InvariantCulture) }
        ]);
    }
}