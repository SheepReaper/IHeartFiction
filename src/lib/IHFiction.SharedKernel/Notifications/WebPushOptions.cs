namespace IHFiction.SharedKernel.Notifications;

public sealed class WebPushOptions
{
    public required string Subject { get; set; }
    public required string PublicKey { get; set; }
    public required string PrivateKey { get; set; }
}
