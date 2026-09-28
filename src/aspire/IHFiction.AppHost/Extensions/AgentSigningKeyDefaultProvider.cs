using System.Security.Cryptography;

using Aspire.Hosting.Publishing;

namespace IHFiction.AppHost.Extensions;

internal static class AgentSigningKeyDefaultProvider
{
    internal sealed class SigningKeyParameterDefault : ParameterDefault
    {
        private string? _value;

        public override string GetDefaultValue()
        {
            if (_value is not null) return _value;
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _value = key.ExportECPrivateKeyPem();
            return _value;
        }

        public override void WriteToManifest(ManifestPublishingContext context) =>
            context.Writer.WriteString("value", GetDefaultValue());
    }

    public static SigningKeyParameterDefault Default => new();
}
