using System.Text.Json;

namespace IHFiction.SharedWeb;

public partial class FictionApiClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        // settings.Converters.Add(new UlidJsonConverter());
    }
}
