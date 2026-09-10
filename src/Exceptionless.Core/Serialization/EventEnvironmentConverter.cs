using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exceptionless.Core.Serialization;

/// <summary>
/// Ignores malformed optional deployment metadata without rejecting an event or its submission batch.
/// </summary>
public sealed class EventEnvironmentConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
