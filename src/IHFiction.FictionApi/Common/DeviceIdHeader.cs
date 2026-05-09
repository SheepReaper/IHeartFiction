using System.Text.RegularExpressions;

namespace IHFiction.FictionApi.Common;

internal static partial class DeviceIdHeader
{
    public const string Name = "X-Device-Id";

    public static bool IsValid(string? deviceId) =>
        !string.IsNullOrWhiteSpace(deviceId)
        && deviceId.Length <= 100
        && DeviceIdRegex().IsMatch(deviceId);

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex DeviceIdRegex();
}