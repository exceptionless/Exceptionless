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

                // Try both deserializers and pick the one that populates more fields.
                // Top-level heuristics can't reliably distinguish structural snake_case
                // (e.g. inner.stack_trace) from user-data underscores (e.g. QueryString keys),
                // so we compare deep property counts to pick the correct format.
                var snakeResult = jsonElement.Deserialize<T>(SnakeCaseOptions);
                var caseResult = jsonElement.Deserialize<T>(CaseInsensitiveOptions);

                // Short-circuit: skip property counting when only one succeeded
                if (snakeResult is null && caseResult is not null)
                    return caseResult;
                if (caseResult is null && snakeResult is not null)
                    return snakeResult;

                int snakeCount = CountPopulatedProperties(snakeResult);
                int caseCount = CountPopulatedProperties(caseResult);

                if (snakeCount >= caseCount && snakeResult is not null)
                    return snakeResult;
                if (caseResult is not null)
                    return caseResult;
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
                    var result = TryDeserializeWithFallback<T>(dictJson, serializer);
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
                    var result = TryDeserializeWithFallback<T>(listJson, serializer);
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
    /// Deserializes JSON trying both snake_case and PascalCase formats, returning whichever
    /// populates more properties. Deep property counting reliably picks the correct format
    /// even when top-level heuristics are ambiguous.
    /// </summary>
    private static T? TryDeserializeWithFallback<T>(string json, ITextSerializer serializer)
    {
        T? snakeResult = default;
        try { snakeResult = serializer.Deserialize<T>(json); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { }

        T? caseResult = default;
        try { caseResult = JsonSerializer.Deserialize<T>(json, CaseInsensitiveOptions); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { }

        int snakeCount = CountPopulatedProperties(snakeResult);
        int caseCount = CountPopulatedProperties(caseResult);

        // Pick the format that captured more data; on tie prefer snake_case (standard format)
        if (snakeCount >= caseCount)
            return snakeResult ?? caseResult;

        return caseResult ?? snakeResult;
    }

    /// <summary>
    /// Maximum recursion depth for deep property counting. Keeps cost bounded
    /// while still capturing enough nested structure to distinguish formats.
    /// </summary>
    private const int MaxRecursionDepth = 3;

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

    /// <summary>
    /// Counts populated properties on an object, recursing into complex sub-objects and
    /// counting collection items. This deep count lets us reliably compare which deserialization
    /// strategy captured more data (e.g. snake_case populates Inner.StackTrace while PascalCase
    /// leaves it empty).
    /// </summary>
    private static int CountPopulatedProperties<T>(T? obj)
    {
        if (obj is null)
            return 0;

        return CountPropertiesDeep(obj, typeof(T), depth: 0);
    }

    private static int CountPropertiesDeep(object obj, Type type, int depth)
    {
        if (depth > MaxRecursionDepth)
            return 0;

        int count = 0;
        foreach (var p in PropertyCache.GetOrAdd(type, t => t.GetProperties()))
        {
            if (!p.CanRead)
                continue;

            try
            {
                // Reflection-based GetValue is acceptable here; this runs on the read path
                // for format detection, not in hot serialization loops.
                object? val = p.GetValue(obj);
                if (val is null or string { Length: 0 })
                    continue;

                count++;

                // Count collection items to distinguish populated vs default-empty collections
                if (val is System.Collections.ICollection collection)
                {
                    count += collection.Count;
                }
                // Recurse into model-type properties (not strings, primitives, or dictionaries)
                else if (val is not string
                         && !p.PropertyType.IsPrimitive
                         && !p.PropertyType.IsEnum
                         && !p.PropertyType.IsValueType
                         && !typeof(System.Collections.IDictionary).IsAssignableFrom(p.PropertyType))
                {
                    count += CountPropertiesDeep(val, p.PropertyType, depth + 1);
                }
            }
            catch (Exception)
            {
                // Ignored
            }
        }

        return count;
    }

    public static void RemoveSensitiveData(this DataDictionary extendedData)
    {
        string[] removeKeys = [.. extendedData.Keys.Where(k => k.StartsWith('-'))];
        foreach (string key in removeKeys)
            extendedData.Remove(key);
    }
}
