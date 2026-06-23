using System.Security.Cryptography;

using Microsoft.AspNetCore.Http;

namespace IHFiction.SharedWeb.Csp;

public sealed class CspNonceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Allocation-optimized 32-character Hex string (16 bytes entropy)
        var nonce = RandomNumberGenerator.GetHexString(32);

        context.Items[CspConstants.HttpContextItemKey] = nonce;

        await next(context);
    }
}
