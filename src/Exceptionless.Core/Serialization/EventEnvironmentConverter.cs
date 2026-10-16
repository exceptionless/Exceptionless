using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Serialization;

/// <summary>
/// Reads valid deployment names into <see cref="Event.Environment"/>. Any other root value keeps
/// its legacy behavior and is stored as custom event data.
/// </summary>
public sealed class EventEnvironmentConverter : JsonConverter<object>
{
    public static void ConfigureProperty(JsonTypeInfo typeInfo)
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
            environment.CustomConverter = new EventEnvironmentConverter();
            environment.Get = property.Get;
            environment.Set = (instance, value) =>
            {
                if (value is JsonElement legacyValue)
                {
                    // Merged into data by Event.OnDeserialized after all properties have been read,
                    // so a later "data" property cannot overwrite it.
                    var ev = (Event)instance;
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
        if (reader.TokenType == JsonTokenType.String && Event.NormalizeEnvironment(reader.GetString()) is not null)
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
