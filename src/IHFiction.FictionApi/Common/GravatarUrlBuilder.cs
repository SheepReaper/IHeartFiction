using System.Security.Cryptography;
using System.Text;

namespace IHFiction.FictionApi.Common;

internal static class GravatarUrlBuilder
{
    public static string? BuildAvatarUrl(string? gravatarEmail)
    {
        if (string.IsNullOrWhiteSpace(gravatarEmail))
        {
            return null;
        }

#pragma warning disable CA1308 // Gravatar canonicalization requires lowercase email and hash
        var normalized = gravatarEmail.Trim().ToLowerInvariant();
#pragma warning restore CA1308

#pragma warning disable CA5351 // MD5 is required by Gravatar hashing protocol
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(normalized));
#pragma warning restore CA5351

#pragma warning disable CA1308 // Gravatar canonicalization requires lowercase email and hash
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
#pragma warning restore CA1308

        return $"https://www.gravatar.com/avatar/{hash}?s=512&d=identicon&r=g";
    }
}
