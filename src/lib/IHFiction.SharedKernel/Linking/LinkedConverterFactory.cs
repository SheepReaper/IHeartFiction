using System.Text.Json;
using System.Text.Json.Serialization;

namespace IHFiction.SharedKernel.Linking;

public sealed class LinkedConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        return typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Linked<>);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        var t = typeToConvert.GetGenericArguments()[0];

        var converterType = typeof(LinkedConverter<>).MakeGenericType(t);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}
