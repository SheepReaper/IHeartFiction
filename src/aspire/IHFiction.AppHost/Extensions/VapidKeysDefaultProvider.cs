using Aspire.Hosting.Publishing;

using WebPush;

namespace IHFiction.AppHost.Extensions;

internal sealed class VapidKeysDefaultProvider
{
    internal sealed class VapidKeyParameterDefault(Func<string> valueGetter) : ParameterDefault
    {
        private string? _value;

        public override string GetDefaultValue() => _value ??= valueGetter();

        public override void WriteToManifest(ManifestPublishingContext context)
        {
            context.Writer.WriteString("value", GetDefaultValue());
        }
    }
    
    private VapidDetails VapidDetails => field ??= VapidHelper.GenerateVapidKeys();
    public VapidKeyParameterDefault DefaultPrivKey => new(() => VapidDetails.PrivateKey);
    public VapidKeyParameterDefault DefaultPubKey => new(() => VapidDetails.PublicKey);
}