using System.Text.Json;
using System.Text.Json.Serialization;

using MongoDB.Bson;

namespace IHFiction.Data.Infrastructure;

public class ObjectIdJsonConverter : JsonConverter<ObjectId>
{
    public override ObjectId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        if (reader.TokenType != JsonTokenType.String && reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException("Expected string");
        }

        var value = reader.GetString();

        if (value?.Length != 24) throw new JsonException("ObjectId invalid: length must be 24");

        return ObjectId.TryParse(value, out var objectId) ? objectId : throw new JsonException("ObjectId invalid: failed to parse");
    }

    public override void Write(Utf8JsonWriter writer, ObjectId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        
        writer.WriteStringValue(value.ToString());
    }
}