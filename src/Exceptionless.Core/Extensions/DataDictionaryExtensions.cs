using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Exceptionless.Core.Models;
using Foundatio.Serializer;

namespace Exceptionless.Core.Extensions;

public static class DataDictionaryExtensions
{
    /// <summary>
    /// Options used when re-serializing in-memory dictionaries to JSON for typed deserialization.
    /// No DictionaryKeyPolicy — dictionary keys are preserved as-is. Typed property name
    /// normalization is handled by <see cref="NormalizeKeysForType"/> below, which only renames
    /// keys at typed-property positions and leaves user-data dictionary keys (Error.Data,
    /// SettingsDictionary, QueryString, etc.) untouched.
    /// </summary>
    private static readonly JsonSerializerOptions s_dictSerializeOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Cache of snake_case-keyed property maps per type, computed once and reused.
    /// Key = snake_case form of the property's JSON name (honoring [JsonPropertyName]).
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> s_propertyMapCache = new();
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
                    // Use lowercase "true"/"false" (JSON convention), not Boolean.TrueString/FalseString
                    // which produce "True"/"False" (PascalCase) and would break JSON consumers.
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
                return serializer.Deserialize<T>(elementJson);
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
                return serializer.Deserialize<T>(jsonString);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                // Ignored - fall through to next handler
            }
        }

        // Dictionary<string, object?> from ObjectToInferredTypesConverter.
        // Serialize to a JsonNode tree, then recursively normalize keys at typed-property
        // positions only (leaving user-data dictionary keys untouched), so the typed
        // deserializer's SnakeCaseLower policy can match multi-word property names from
        // legacy PascalCase data (e.g., "ClientIpAddress" → "client_ip_address").
        if (data is Dictionary<string, object?> dictionary)
        {
            try
            {
                JsonNode? node = JsonSerializer.SerializeToNode(dictionary, s_dictSerializeOptions);
                NormalizeKeysForType(node, typeof(T));
                string dictJson = node?.ToJsonString() ?? "{}";
                return serializer.Deserialize<T>(dictJson);
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
                    return serializer.Deserialize<T>(listJson);
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

    /// <summary>
    /// Recursively walks a <see cref="JsonNode"/> tree and renames object keys to their
    /// snake_case form ONLY at positions corresponding to typed properties of <paramref name="targetType"/>.
    /// Dictionary&lt;string, T&gt; positions are recursed into (for typed values) but their keys are
    /// preserved as-is so user-provided keys (Error.Data, QueryString, Cookies, SettingsDictionary, etc.)
    /// are never modified.
    /// </summary>
    /// <remarks>
    /// This bridges legacy PascalCase data (e.g., from V1 client submissions stored before STJ migration)
    /// to the STJ deserializer's SnakeCaseLower naming policy. STJ's PropertyNameCaseInsensitive only
    /// handles case differences ("Message" ↔ "message"); it cannot match structurally different forms
    /// ("ClientIpAddress" vs "client_ip_address" — no underscores in input).
    /// </remarks>
    private static void NormalizeKeysForType(JsonNode? node, Type targetType)
    {
        if (node is null)
            return;

        // Unwrap Nullable<T>
        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (node is JsonArray arr)
        {
            Type? elementType = GetCollectionElementType(targetType);
            if (elementType is null)
                return;
            foreach (JsonNode? item in arr)
                NormalizeKeysForType(item, elementType);
            return;
        }

        if (node is not JsonObject obj)
            return;

        // Dictionary<string, X>: preserve keys (user data), recurse into values with X type.
        if (TryGetDictionaryValueType(targetType, out Type? dictValueType))
        {
            foreach (KeyValuePair<string, JsonNode?> kvp in obj)
                NormalizeKeysForType(kvp.Value, dictValueType!);
            return;
        }

        // Primitives / strings / values aren't expected as JsonObject targets.
        if (targetType.IsPrimitive || targetType == typeof(string) || targetType == typeof(decimal) ||
            targetType == typeof(DateTime) || targetType == typeof(DateTimeOffset) || targetType == typeof(Guid) ||
            targetType == typeof(TimeSpan) || targetType == typeof(object))
        {
            return;
        }

        // Typed model: rename keys to their snake_case form when they match a property
        // (case-insensitively, after snake-casing). Recurse into typed property values.
        Dictionary<string, PropertyInfo> properties = GetPropertyMap(targetType);
        var renames = new List<(string oldKey, string newKey)>();
        var recurses = new List<(string key, Type type)>();

        foreach (KeyValuePair<string, JsonNode?> kvp in obj)
        {
            string snakeKey = JsonNamingPolicy.SnakeCaseLower.ConvertName(kvp.Key);
            if (!properties.TryGetValue(snakeKey, out PropertyInfo? prop))
                continue;
            if (!string.Equals(kvp.Key, snakeKey, StringComparison.Ordinal))
                renames.Add((kvp.Key, snakeKey));
            recurses.Add((snakeKey, prop.PropertyType));
        }

        foreach ((string oldKey, string newKey) in renames)
        {
            JsonNode? value = obj[oldKey];
            obj.Remove(oldKey);
            // Detach value from parent before reassigning (JsonNode can only have one parent).
            if (value is not null && value.Parent is not null)
                value = value.DeepClone();
            obj[newKey] = value;
        }

        foreach ((string key, Type propType) in recurses)
            NormalizeKeysForType(obj[key], propType);
    }

    private static Dictionary<string, PropertyInfo> GetPropertyMap(Type type)
    {
        return s_propertyMapCache.GetOrAdd(type, static t =>
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (PropertyInfo prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite && !IsCollectionLike(prop.PropertyType))
                    continue;
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.Always })
                    continue;
                string name = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
                string snakeName = JsonNamingPolicy.SnakeCaseLower.ConvertName(name);
                map[snakeName] = prop;
            }
            return map;
        });
    }

    private static bool IsCollectionLike(Type type)
    {
        if (type == typeof(string))
            return false;
        return typeof(IEnumerable).IsAssignableFrom(type);
    }

    private static Type? GetCollectionElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];
        foreach (Type iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return iface.GetGenericArguments()[0];
        }
        return null;
    }

    private static bool TryGetDictionaryValueType(Type type, out Type? valueType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
        {
            valueType = type.GetGenericArguments()[1];
            return true;
        }
        foreach (Type iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                valueType = iface.GetGenericArguments()[1];
                return true;
            }
        }
        valueType = null;
        return false;
    }
}
