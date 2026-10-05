using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Serialization;

/// <summary>
/// Reads deployment names while preserving legacy root values as custom event data.
/// </summary>
public sealed class EventEnvironmentConverter : JsonConverter<object>
{
    private bool PreserveStringValues { get; init; }

    public override bool HandleNull => true;

    public static void ConfigureProperty(JsonTypeInfo typeInfo)
    {
        ConfigureProperty(typeInfo, preserveStringValues: false);
    }

    public static void ConfigureIngestionProperty(JsonTypeInfo typeInfo)
    {
        ConfigureProperty(typeInfo, preserveStringValues: true);
    }

    private static void ConfigureProperty(JsonTypeInfo typeInfo, bool preserveStringValues)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object || !typeof(Event).IsAssignableFrom(typeInfo.Type))
        {
            return;
        }

        for (int i = 0; i < typeInfo.Properties.Count; i++)
        {
            var property = typeInfo.Properties[i];
            if (property.AttributeProvider is not PropertyInfo { Name: nameof(Event.Environment) })
            {
                continue;
            }

            var environment = typeInfo.CreateJsonPropertyInfo(typeof(object), property.Name);
            environment.AttributeProvider = property.AttributeProvider;
            environment.CustomConverter = new EventEnvironmentConverter { PreserveStringValues = preserveStringValues };
            environment.Get = property.Get;
            environment.Set = (instance, value) =>
            {
                if (value is JsonElement legacyValue)
                {
                    var ev = (Event)instance;
                    ev.Environment = legacyValue.ValueKind == JsonValueKind.String ? legacyValue.GetString() : null;
                    // Merge after all properties have been read so a later "data" property cannot overwrite it.
                    ev.ExtensionData ??= [];
                    ev.ExtensionData[property.Name] = legacyValue;
                }
                else
                {
                    property.Set!(instance, value);
                }
            };
            typeInfo.Properties[i] = environment;
            return;
        }
    }

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && !PreserveStringValues)
        {
            return reader.GetString();
        }

        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.Clone();
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        writer.WriteStringValue((string?)value);
    }
}
