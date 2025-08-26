using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IHFiction.SharedKernel.Linking;

public sealed class LinkedConverter<T> : JsonConverter<Linked<T>>
{
    public override Linked<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        using var doc = JsonDocument.ParseValue(ref reader);

        IEnumerable<LinkItem>? links = null;

        using MemoryStream objWriter = new();

        using (Utf8JsonWriter utf8 = new(objWriter))
        {
            utf8.WriteStartObject();

            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.NameEquals(options.PropertyNamingPolicy?.ConvertName(nameof(ILinks.Links)) ?? nameof(ILinks.Links)))
                {
                    links = JsonSerializer.Deserialize<IEnumerable<LinkItem>>(p.Value.GetRawText(), options);
                }
                else
                {
                    p.WriteTo(utf8);
                }
            }
            utf8.WriteEndObject();
        }

        var json = Encoding.UTF8.GetString(objWriter.ToArray());
        var value = JsonSerializer.Deserialize<T>(json, options)!;

        return new Linked<T>(value, links ?? []);
    }

    public override void Write(Utf8JsonWriter writer, Linked<T> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        writer.WriteStartObject();

        var inner = JsonSerializer.SerializeToElement(value.Value, options);

        foreach (var p in inner.EnumerateObject()) p.WriteTo(writer);

        var name = options.PropertyNamingPolicy?.ConvertName(nameof(ILinks.Links)) ?? nameof(ILinks.Links);

        writer.WritePropertyName(name);

        JsonSerializer.Serialize(writer, value.Links, options);

        writer.WriteEndObject();
    }
}