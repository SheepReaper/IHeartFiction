using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;

namespace IHFiction.ApiClientGenerator;

internal sealed class CustomFormatTypeResolver(CSharpGeneratorSettings settings) : CSharpTypeResolver(settings)
{
    public override string Resolve(JsonSchema schema, bool isNullable, string? typeNameHint)
    {
        var actual = schema.ActualTypeSchema;

        if (actual.Type != JsonObjectType.String || actual.IsEnumeration)
            return base.Resolve(schema, isNullable, typeNameHint);

        return actual.Format switch
        {
            "objectid" => isNullable ? "MongoDB.Bson.ObjectId?" : "MongoDB.Bson.ObjectId",
            "ulid" => isNullable ? "System.Ulid?" : "System.Ulid",
            _ => base.Resolve(schema, isNullable, typeNameHint)
        };
    }
}