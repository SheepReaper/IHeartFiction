namespace IHFiction.Data.Authors.Domain;

public sealed class Profile
{
    public string? Bio { get; set; }

    private ICollection<SocialLink>? _socialLinks;
    public ICollection<SocialLink> SocialLinks => _socialLinks ??= [];
}

public sealed class SocialLink
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}