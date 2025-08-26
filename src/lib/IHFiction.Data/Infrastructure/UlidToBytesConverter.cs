using Microsoft.EntityFrameworkCore.Storage.ValueConversion;


namespace IHFiction.Data.Infrastructure;

internal sealed class UlidToBytesConverter(ConverterMappingHints? mappingHints) : ValueConverter<Ulid, byte[]>(
    convertToProviderExpression: id => id.ToByteArray(),
    convertFromProviderExpression: bytes => new Ulid(bytes),
    mappingHints: DefaultHints.With(mappingHints)
    )
{
    public UlidToBytesConverter() : this(null)
    {
    }
    private static readonly ConverterMappingHints DefaultHints = new(size: 16);
}
