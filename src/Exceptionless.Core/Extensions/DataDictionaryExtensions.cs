using System.Text.Json;
using System.Text.Json.Nodes;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Extensions;

public static class DataDictionaryExtensions
{
    /// <summary>
    /// Retrieves a typed value from the <see cref="DataDictionary"/>, deserializing if necessary.
    /// </summary>
    /// <typeparam name="T">The target type to deserialize to.</typeparam>
    /// <param name="extendedData">The data dictionary containing the value.</param>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="options">The JSON serializer options to use for deserialization.</param>
    /// <returns>The deserialized value, or <c>default</c> if deserialization fails.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found in the dictionary.</exception>
    /// <remarks>
    /// <para>This method handles multiple source formats in priority order:</para>
    /// <list type="number">
    ///   <item><description>Direct type match - returns value directly</description></item>
    ///   <item><description><see cref="JsonDocument"/> - extracts root element and deserializes</description></item>
    ///   <item><description><see cref="JsonElement"/> - deserializes using provided options</description></item>
    ///   <item><description><see cref="JsonNode"/> - deserializes using provided options</description></item>
    ///   <item><description><see cref="Dictionary{TKey,TValue}"/> - re-serializes to JSON then deserializes (for ObjectToInferredTypesConverter output)</description></item>
    ///   <item><description><see cref="List{T}"/> of objects - re-serializes to JSON then deserializes</description></item>
    ///   <item><description><see cref="Newtonsoft.Json.Linq.JObject"/> - uses ToObject for Elasticsearch compatibility (data read from Elasticsearch uses JSON.NET)</description></item>
    ///   <item><description>JSON string - parses and deserializes</description></item>
    ///   <item><description>Fallback - attempts type conversion via ToType</description></item>
    /// </list>
    /// </remarks>
    public static T? GetValue<T>(this DataDictionary extendedData, string key, JsonSerializerOptions options)
    {
        if (!extendedData.TryGetValue(key, out object? data))
            throw new KeyNotFoundException($"Key \"{key}\" not found in the dictionary.");

        if (data is T value)
            return value;

        // JsonDocument -> JsonElement
        if (data is JsonDocument jsonDocument)
            data = jsonDocument.RootElement;

        // JsonElement (from STJ deserialization when ObjectToInferredTypesConverter wasn't used)
        if (data is JsonElement jsonElement &&
            TryDeserialize(jsonElement, options, out T? jsonElementResult))
        {
            return jsonElementResult;
        }

        // JsonNode (JsonObject/JsonArray/JsonValue)
        if (data is JsonNode jsonNode)
        {
            try
            {
                var result = jsonNode.Deserialize<T>(options);
                if (result is not null)
                    return result;
            }
            catch
            {
                // Ignored - fall through to next handler
            }
        }

        // Dictionary<string, object?> from ObjectToInferredTypesConverter
        // Re-serialize to JSON then deserialize to target type with proper naming policy
        if (data is Dictionary<string, object?> dictionary)
        {
            try
            {
                string dictJson = JsonSerializer.Serialize(dictionary, options);
                var result = JsonSerializer.Deserialize<T>(dictJson, options);
                if (result is not null)
                    return result;
            }
            catch
            {
                // Ignored - fall through to next handler
            }
        }

        // List<object?> from ObjectToInferredTypesConverter (for array values)
        if (data is List<object?> list)
        {
            try
            {
                string listJson = JsonSerializer.Serialize(list, options);
                var result = JsonSerializer.Deserialize<T>(listJson, options);
                if (result is not null)
                    return result;
            }
            catch
            {
                // Ignored - fall through to next handler
            }
        }

        // Newtonsoft.Json.Linq.JObject - for Elasticsearch compatibility.
        // When data is read from Elasticsearch (which uses JSON.NET via NEST), complex objects
        // in DataDictionary are deserialized as JObject. This handler converts them to the target type.
        if (data is Newtonsoft.Json.Linq.JObject jObject)
        {
            try
            {
                return jObject.ToObject<T>();
            }
            catch
            {
                // Ignored - fall through to next handler
            }
        }

        // JSON string
        if (data is string json && json.IsJson())
        {
            try
            {
                var result = JsonSerializer.Deserialize<T>(json, options);
                if (result is not null)
                    return result;
            }
            catch
            {
                // Ignored - fall through to next handler
            }
        }

        // Fallback: attempt direct type conversion
        try
        {
            if (data != null)
            {
                return data.ToType<T>();
            }
        }
        catch
        {
            // Ignored
        }

        return default;
    }

    private static bool TryDeserialize<T>(JsonElement element, JsonSerializerOptions options, out T? result)
    {
        result = default;

        try
        {
            // Fast-path for common primitives where the element isn't an object/array
            // (Deserialize<T> also works for these, but this avoids some edge cases and allocations)
            if (typeof(T) == typeof(string))
            {
                object? s = element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => element.GetRawText()
                };

                result = (T?)s;
                return true;
            }

            // General case
            var deserialized = element.Deserialize<T>(options);
            if (deserialized is not null)
            {
                result = deserialized;
                return true;
            }
        }
        catch
        {
            // Ignored
        }

        return false;
    }

    public static void RemoveSensitiveData(this DataDictionary extendedData)
    {
        string[] removeKeys = extendedData.Keys.Where(k => k.StartsWith('-')).ToArray();
        foreach (string key in removeKeys)
            extendedData.Remove(key);
    }
}
