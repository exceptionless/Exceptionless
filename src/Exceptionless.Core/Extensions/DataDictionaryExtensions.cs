using System.Text.Json;
using System.Text.Json.Nodes;
using Exceptionless.Core.Models;
using Foundatio.Serializer;

namespace Exceptionless.Core.Extensions;

public static class DataDictionaryExtensions
{
    /// <summary>
    /// Fallback options for deserializing JSON without a naming policy (case-insensitive matching).
    /// </summary>
    private static readonly JsonSerializerOptions CaseInsensitiveFallbackOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Attempts deserialization using the primary serializer (snake_case naming policy),
    /// then falls back to a case-insensitive deserializer as a safety net.
    /// Picks whichever result populated more properties (measured by serialized output length).
    /// </summary>
    private static T? TryDeserializeWithFallback<T>(string json, ITextSerializer serializer)
    {
        var primary = serializer.Deserialize<T>(json);

        if (primary is null)
            return JsonSerializer.Deserialize<T>(json, CaseInsensitiveFallbackOptions);

        var fallback = JsonSerializer.Deserialize<T>(json, CaseInsensitiveFallbackOptions);
        if (fallback is not null)
        {
            // Serialize both results with the SAME serializer for fair comparison.
            // The result with more populated (non-null) properties produces longer output.
            string primaryJson = serializer.SerializeToString(primary) ?? "";
            string fallbackJson = serializer.SerializeToString(fallback) ?? "";
            return fallbackJson.Length > primaryJson.Length ? fallback : primary;
        }

        return primary;
    }

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

                string elementJson = jsonElement.GetRawText();
                return TryDeserializeWithFallback<T>(elementJson, serializer);
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
                return TryDeserializeWithFallback<T>(jsonString, serializer);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                // Ignored - fall through to next handler
            }
        }

        // Dictionary<string, object?> from ObjectToInferredTypesConverter.
        if (data is Dictionary<string, object?> dictionary)
        {
            try
            {
                string? dictJson = serializer.SerializeToString(dictionary);
                if (dictJson is not null)
                {
                    return TryDeserializeWithFallback<T>(dictJson, serializer);
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
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
                    return TryDeserializeWithFallback<T>(listJson, serializer);
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                // Ignored - fall through to next handler
            }
        }

        // JSON string - deserialize via ITextSerializer.
        if (data is string json && json.IsJson())
        {
            try
            {
                return serializer.Deserialize<T>(json);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException)
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
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or InvalidCastException or ArgumentException)
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
