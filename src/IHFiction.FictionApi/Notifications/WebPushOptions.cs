#pragma warning disable CA1515 // Options are injected into a public Wolverine handler constructor.
namespace IHFiction.FictionApi.Notifications;

public sealed class WebPushOptions
{
    public required string Subject { get; set; }
    public required string PublicKey { get; set; }
    public required string PrivateKey { get; set; }
}
#pragma warning restore CA1515