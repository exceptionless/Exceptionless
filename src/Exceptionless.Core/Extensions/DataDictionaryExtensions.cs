using System.Text.Json;
using System.Text.Json.Nodes;
using Exceptionless.Core.Models;
using Foundatio.Serializer;

namespace Exceptionless.Core.Extensions;

public static class DataDictionaryExtensions
{
    /// <summary>
    /// Fallback options for deserializing legacy PascalCase JSON without a naming policy.
    /// Without a naming policy, C# property names match JSON keys directly (case-insensitively),
    /// which handles PascalCase data that the snake_case primary serializer cannot match.
    /// </summary>
    private static readonly JsonSerializerOptions CaseInsensitiveFallbackOptions = new()
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

                string elementJson = jsonElement.GetRawText();
                var primary = serializer.Deserialize<T>(elementJson);

                if (primary is null)
                    return JsonSerializer.Deserialize<T>(elementJson, CaseInsensitiveFallbackOptions);

                var fallback = JsonSerializer.Deserialize<T>(elementJson, CaseInsensitiveFallbackOptions);
                if (fallback is not null)
                {
                    string primaryJson = serializer.SerializeToString(primary) ?? "";
                    string fallbackJson = serializer.SerializeToString(fallback) ?? "";
                    return fallbackJson.Length > primaryJson.Length ? fallback : primary;
                }

                return primary;
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
                var primary = serializer.Deserialize<T>(jsonString);

                if (primary is null)
                    return JsonSerializer.Deserialize<T>(jsonString, CaseInsensitiveFallbackOptions);

                var fallback = JsonSerializer.Deserialize<T>(jsonString, CaseInsensitiveFallbackOptions);
                if (fallback is not null)
                {
                    string primaryJson = serializer.SerializeToString(primary) ?? "";
                    string fallbackJson = serializer.SerializeToString(fallback) ?? "";
                    return fallbackJson.Length > primaryJson.Length ? fallback : primary;
                }

                return primary;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                // Ignored - fall through to next handler
            }
        }

        // Dictionary<string, object?> from ObjectToInferredTypesConverter.
        // Dictionary keys preserve the original JSON casing, which may be snake_case (current format)
        // or PascalCase (legacy data). The primary serializer (snake_case naming policy) handles
        // snake_case keys; the fallback (no naming policy, case-insensitive) handles PascalCase.
        // We try both and pick the one that populated more properties (longer serialized output).
        if (data is Dictionary<string, object?> dictionary)
        {
            try
            {
                string? dictJson = serializer.SerializeToString(dictionary);
                if (dictJson is not null)
                {
                    // Try primary serializer (snake_case naming policy) first.
                    var primary = serializer.Deserialize<T>(dictJson);

                    // Fast-path: if primary is null, try fallback.
                    if (primary is null)
                        return JsonSerializer.Deserialize<T>(dictJson, CaseInsensitiveFallbackOptions);

                    // Both might be non-null — check which one actually populated properties
                    // by comparing serialized lengths (longer = more properties matched).
                    var fallback = JsonSerializer.Deserialize<T>(dictJson, CaseInsensitiveFallbackOptions);
                    if (fallback is not null)
                    {
                        string primaryJson = serializer.SerializeToString(primary) ?? "";
                        string fallbackJson = serializer.SerializeToString(fallback) ?? "";
                        return fallbackJson.Length > primaryJson.Length ? fallback : primary;
                    }

                    return primary;
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
                    var primary = serializer.Deserialize<T>(listJson);

                    if (primary is null)
                        return JsonSerializer.Deserialize<T>(listJson, CaseInsensitiveFallbackOptions);

                    var fallback = JsonSerializer.Deserialize<T>(listJson, CaseInsensitiveFallbackOptions);
                    if (fallback is not null)
                    {
                        string primaryJson = serializer.SerializeToString(primary) ?? "";
                        string fallbackJson = serializer.SerializeToString(fallback) ?? "";
                        return fallbackJson.Length > primaryJson.Length ? fallback : primary;
                    }

                    return primary;
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
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
