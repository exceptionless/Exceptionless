using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exceptionless.Web.Utility;

/// <summary>
/// JsonConverterFactory for Delta&lt;T&gt; types to support System.Text.Json deserialization.
/// </summary>
public class DeltaJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType)
        {
            return false;
        }

        return typeToConvert.GetGenericTypeDefinition() == typeof(Delta<>);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var entityType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(DeltaJsonConverter<>).MakeGenericType(entityType);

        return (JsonConverter?)Activator.CreateInstance(converterType, options);
    }
}

/// <summary>
/// JsonConverter for Delta&lt;T&gt; that reads JSON properties and sets them on the Delta instance.
/// </summary>
public class DeltaJsonConverter<TEntityType> : JsonConverter<Delta<TEntityType>> where TEntityType : class
{
    private readonly JsonSerializerOptions _options;
    private readonly Dictionary<string, string> _jsonNameToPropertyName;

    public DeltaJsonConverter(JsonSerializerOptions options)
    {
        // Create a copy without the converter to avoid infinite recursion
        _options = new JsonSerializerOptions(options);

        // Build a mapping from JSON property names (snake_case) to C# property names (PascalCase)
        _jsonNameToPropertyName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entityType = typeof(TEntityType);
        foreach (var prop in entityType.GetProperties())
        {
            var jsonName = options.PropertyNamingPolicy?.ConvertName(prop.Name) ?? prop.Name;
            _jsonNameToPropertyName[jsonName] = prop.Name;
        }
    }

    public override Delta<TEntityType>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        var delta = new Delta<TEntityType>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            var jsonPropertyName = reader.GetString();
            if (jsonPropertyName is null)
            {
                throw new JsonException("Property name is null");
            }

            reader.Read();

            // Convert JSON property name (snake_case) to C# property name (PascalCase)
            var propertyName = _jsonNameToPropertyName.TryGetValue(jsonPropertyName, out var mapped)
                ? mapped
                : jsonPropertyName;

            // Try to get the property type from Delta
            if (delta.TryGetPropertyType(propertyName, out var propertyType) && propertyType is not null)
            {
                var value = JsonSerializer.Deserialize(ref reader, propertyType, _options);
                delta.TrySetPropertyValue(propertyName, value);
            }
            else
            {
                // Unknown property - read and store as JsonElement
                var element = JsonSerializer.Deserialize<JsonElement>(ref reader, _options);
                delta.UnknownProperties[jsonPropertyName] = element;
            }
        }

        return delta;
    }

    public override void Write(Utf8JsonWriter writer, Delta<TEntityType> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var (propertyName, propertyValue) in value.GetChangedPropertyNames()
            .Select(name => (Name: name, HasValue: value.TryGetPropertyValue(name, out var val), Value: val))
            .Where(x => x.HasValue)
            .Select(x => (x.Name, x.Value)))
        {
            // Convert property name to snake_case if needed
            var jsonPropertyName = options.PropertyNamingPolicy?.ConvertName(propertyName) ?? propertyName;
            writer.WritePropertyName(jsonPropertyName);
            JsonSerializer.Serialize(writer, propertyValue, _options);
        }

        foreach (var kvp in value.UnknownProperties)
        {
            var jsonPropertyName = options.PropertyNamingPolicy?.ConvertName(kvp.Key) ?? kvp.Key;
            writer.WritePropertyName(jsonPropertyName);
            JsonSerializer.Serialize(writer, kvp.Value, _options);
        }

        writer.WriteEndObject();
    }
}
