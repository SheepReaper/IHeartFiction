using System.Text.Json;

using Cysharp.Serialization.Json;

using IHFiction.Data.Infrastructure;

namespace IHFiction.SharedWeb;

public partial class FictionApiClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.Converters.Add(new UlidJsonConverter());
        settings.Converters.Add(new ObjectIdJsonConverter());
    }}

public partial class FileParameter(System.IO.Stream data, string? fileName, string? contentType){
    public FileParameter(System.IO.Stream data) : this(data, null, null) {}
    public FileParameter(System.IO.Stream data, string? fileName) : this(data, fileName, null) {}
    public System.IO.Stream Data => data;
    public string? FileName => fileName;
    public string? ContentType => contentType;
}