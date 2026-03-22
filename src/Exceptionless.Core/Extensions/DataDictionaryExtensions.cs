using System.Text.Json;
using System.Text.Json.Nodes;
using Exceptionless.Core.Models;
using Exceptionless.Core.Serialization;
using Foundatio.Serializer;

namespace Exceptionless.Core.Extensions;

public static class DataDictionaryExtensions
{
    /// <summary>
    /// Options for deserializing JsonElement values with snake_case property names (standard format).
    /// Uses the naming policy to map C# PascalCase names to snake_case JSON names.
    /// </summary>
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = LowerCaseUnderscoreNamingPolicy.Instance
    };

    /// <summary>
    /// Fallback options for deserializing JsonElement values with PascalCase property names.
    /// Handles legacy or non-standard input where property names match C# names directly.
    /// </summary>
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    /// <summary>
    /// Retrieves a typed value from the <see cref="DataDictionary"/>, deserializing if necessary.
    /// </summary>
    /// <typeparam name="T">The target type to deserialize to.</typeparam>
    /// <param name="extendedData">The data dictionary containing the value.</param>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="serializer">The text serializer to use for deserialization.</param>
    /// <returns>The deserialized value, or <c>default</c> if deserialization fails.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not found in the dictionary.</exception>
    /// <remarks>
    /// <para>This method handles multiple source formats in priority order:</para>
    /// <list type="number">
    ///   <item><description>Direct type match - returns value directly</description></item>
    ///   <item><description><see cref="JsonDocument"/> - extracts root element and deserializes</description></item>
    ///   <item><description><see cref="JsonElement"/> - extracts raw JSON and deserializes via ITextSerializer</description></item>
    ///   <item><description><see cref="JsonNode"/> - extracts JSON string and deserializes via ITextSerializer</description></item>
    ///   <item><description><see cref="Dictionary{TKey,TValue}"/> - re-serializes to JSON then deserializes via ITextSerializer</description></item>
    ///   <item><description><see cref="List{T}"/> of objects - re-serializes to JSON then deserializes via ITextSerializer</description></item>
    ///   <item><description><see cref="Newtonsoft.Json.Linq.JObject"/> - uses ToObject for Elasticsearch compatibility</description></item>
    ///   <item><description>JSON string - deserializes via ITextSerializer</description></item>
    ///   <item><description>Fallback - attempts type conversion via ToType</description></item>
    /// </list>
    /// </remarks>
    public static T? GetValue<T>(this DataDictionary extendedData, string key, ITextSerializer serializer)
    {
        if (!extendedData.TryGetValue(key, out object? data))
            throw new KeyNotFoundException($"Key \"{key}\" not found in the dictionary.");

        if (data is T value)
            return value;

        // JsonDocument -> JsonElement
        if (data is JsonDocument jsonDocument)
            data = jsonDocument.RootElement;

        // JsonElement (from STJ deserialization when ObjectToInferredTypesConverter wasn't used)
        if (data is JsonElement jsonElement)
        {
            try
            {
                // Fast-path for string type
                if (typeof(T) == typeof(string))
                {
                    object? s = jsonElement.ValueKind switch
                    {
                        JsonValueKind.String => jsonElement.GetString(),
                        JsonValueKind.Number => jsonElement.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => null,
                        _ => jsonElement.GetRawText()
                    };

                    return (T?)s;
                }

                // Deserialize from JsonElement, trying snake_case naming first (standard format)
                // then falling back to PascalCase for legacy/non-standard input.
                var result = jsonElement.Deserialize<T>(SnakeCaseOptions);
                if (result is not null)
                    return result;

                result = jsonElement.Deserialize<T>(CaseInsensitiveOptions);
                if (result is not null)
                    return result;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                // Ignored - fall through to next handler
            }
        }

        // JsonNode (JsonObject/JsonArray/JsonValue)
        if (data is JsonNode jsonNode)
        {
            try
            {
                string jsonString = jsonNode.ToJsonString();
                var result = serializer.Deserialize<T>(jsonString);
                if (result is not null)
                    return result;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                // Ignored - fall through to next handler
            }
        }

        // Dictionary<string, object?> from ObjectToInferredTypesConverter
        // Re-serialize to JSON then deserialize to target type via ITextSerializer
        if (data is Dictionary<string, object?> dictionary)
        {
            try
            {
                string? dictJson = serializer.SerializeToString(dictionary);
                if (dictJson is not null)
                {
                    var result = serializer.Deserialize<T>(dictJson);
                    if (result is not null)
                        return result;
                }
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
                string? listJson = serializer.SerializeToString(list);
                if (listJson is not null)
                {
                    var result = serializer.Deserialize<T>(listJson);
                    if (result is not null)
                        return result;
                }
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

        // JSON string - deserialize via ITextSerializer
        if (data is string json && json.IsJson())
        {
            try
            {
                var result = serializer.Deserialize<T>(json);
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

    public static void RemoveSensitiveData(this DataDictionary extendedData)
    {
        string[] removeKeys = [.. extendedData.Keys.Where(k => k.StartsWith('-'))];
        foreach (string key in removeKeys)
            extendedData.Remove(key);
    }
}
