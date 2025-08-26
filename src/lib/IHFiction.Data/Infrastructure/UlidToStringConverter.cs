using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IHFiction.Data.Infrastructure;

internal sealed class UlidToStringConverter(ConverterMappingHints? mappingHints) : ValueConverter<Ulid, string>(
    convertToProviderExpression: id => id.ToString(),
    convertFromProviderExpression: s => Ulid.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
    mappingHints: DefaultHints.With(mappingHints)
    )
{
    public UlidToStringConverter() : this(null)
    {
    }
    private static readonly ConverterMappingHints DefaultHints = new(size: 26);
}
