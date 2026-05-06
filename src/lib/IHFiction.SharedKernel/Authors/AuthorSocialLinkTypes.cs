namespace IHFiction.SharedKernel.Authors;

public static class AuthorSocialLinkTypes
{
    public const string Patreon = "Patreon";

    public static IReadOnlyList<string> SponsorshipPlatforms { get; } =
    [
        Patreon
    ];

    public static IReadOnlyList<string> SupportedTypes { get; } = SponsorshipPlatforms;

    public static bool IsSupported(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && SupportedTypes.Contains(value, StringComparer.OrdinalIgnoreCase);
}
