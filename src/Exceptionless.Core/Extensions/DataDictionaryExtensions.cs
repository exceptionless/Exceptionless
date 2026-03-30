using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;
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
        PropertyNamingPolicy = LowerCaseUnderscoreNamingPolicy.Instance,
        Converters = { new ObjectToInferredTypesConverter() }
    };

    /// <summary>
    /// Fallback options for deserializing JsonElement values with PascalCase property names.
    /// Handles legacy or non-standard input where property names match C# names directly.
    /// Includes the same converters as the DI-registered options for consistent nested deserialization.
    /// Note: Intentionally omits IncludeFields and RespectNullableAnnotations from the DI-registered
    /// options because this is a fallback deserializer for data-dictionary values, not the primary
    /// API serializer. Only the settings needed for correct property mapping are included.
    /// </summary>
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.Create(new TextEncoderSettings(UnicodeRanges.All)),
        Converters = { new ObjectToInferredTypesConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { EmptyCollectionModifier.SkipEmptyCollections }
        }
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

                // Detect format from property names: underscores indicate snake_case,
                // absence indicates PascalCase. Use the matching deserializer first to avoid
                // partial matches (e.g. single-word PascalCase props matching snake_case).
                var primaryOptions = LooksLikeSnakeCase(jsonElement) ? SnakeCaseOptions : CaseInsensitiveOptions;
                var result = jsonElement.Deserialize<T>(primaryOptions);
                if (CountPopulatedProperties(result) > 0)
                    return result;

                // Fallback: try the other format if primary produced nothing
                var fallbackOptions = primaryOptions == SnakeCaseOptions ? CaseInsensitiveOptions : SnakeCaseOptions;
                var fallbackResult = jsonElement.Deserialize<T>(fallbackOptions);
                if (CountPopulatedProperties(fallbackResult) > 0)
                    return fallbackResult;

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
        // Re-serialize to JSON then deserialize to target type.
        // Dictionary keys are literal (not naming-policy-transformed), so they may be PascalCase
        // (legacy) or snake_case (standard). Detect format from keys to avoid partial matches.
        if (data is Dictionary<string, object?> dictionary)
        {
            try
            {
                string? dictJson = serializer.SerializeToString(dictionary);
                if (dictJson is not null)
                {
                    bool isSnakeCase = dictionary.Keys.Any(k => k.Contains('_'));
                    var result = TryDeserializeWithFallback<T>(dictJson, serializer, preferSnakeCase: isSnakeCase);
                    if (result is not null)
                        return result;
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
                    bool isSnakeCase = list.OfType<Dictionary<string, object?>>() 
                        .SelectMany(d => d.Keys)
                        .Any(k => k.Contains('_'));
                    var result = TryDeserializeWithFallback<T>(listJson, serializer, preferSnakeCase: isSnakeCase);
                    if (result is not null)
                        return result;
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

    /// <summary>
    /// Deserializes JSON using the preferred format first, falling back to the other format
    /// only if the primary produced zero populated properties. Avoids double-deserialization
    /// overhead and partial-match issues with single-word PascalCase properties matching snake_case.
    /// </summary>
    private static T? TryDeserializeWithFallback<T>(string json, ITextSerializer serializer, bool preferSnakeCase = true)
    {
        T? result = default;
        try
        {
            result = preferSnakeCase
                ? serializer.Deserialize<T>(json)
                : JsonSerializer.Deserialize<T>(json, CaseInsensitiveOptions);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { }

        if (CountPopulatedProperties(result) > 0)
            return result;

        // Fallback: try the other format
        T? fallback = default;
        try
        {
            fallback = preferSnakeCase
                ? JsonSerializer.Deserialize<T>(json, CaseInsensitiveOptions)
                : serializer.Deserialize<T>(json);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { }

        return CountPopulatedProperties(fallback) > 0 ? fallback : result;
    }

    /// <summary>
    /// Detects whether a JsonElement's property names use snake_case naming.
    /// Returns true if any top-level property name contains an underscore.
    /// </summary>
    private static bool LooksLikeSnakeCase(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name.Contains('_'))
                return true;
        }

        return false;
    }

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <summary>
    /// Counts the number of non-null, non-empty properties on an object.
    /// Used to compare which deserialization strategy captured more data.
    /// </summary>
    private static int CountPopulatedProperties<T>(T? obj)
    {
        if (obj is null)
            return 0;

        return PropertyCache.GetOrAdd(typeof(T), t => t.GetProperties())
            .Count(p =>
            {
                if (!p.CanRead)
                    return false;

                try
                {
                    object? val = p.GetValue(obj);
                    return val is not null and not (string { Length: 0 });
                }
                catch
                {
                    return false;
                }
            });
    }

    public static void RemoveSensitiveData(this DataDictionary extendedData)
    {
        string[] removeKeys = [.. extendedData.Keys.Where(k => k.StartsWith('-'))];
        foreach (string key in removeKeys)
            extendedData.Remove(key);
    }
}
