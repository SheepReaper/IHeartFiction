using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IHFiction.FictionApi.Stories;

internal static class ReadIdentity
{
    public static string ForUser(Guid userId) => $"u:{userId.ToString("D", CultureInfo.InvariantCulture).ToUpperInvariant()}";

    public static string ForDevice(string deviceId)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(deviceId));
        return $"d:{Convert.ToHexStringLower(digest)}";
    }
}
