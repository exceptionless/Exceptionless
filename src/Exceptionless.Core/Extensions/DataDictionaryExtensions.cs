using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Exceptionless.Core.Models;
using Exceptionless.Core.Serialization;
using Foundatio.Serializer;

namespace Exceptionless.Core.Extensions;

public static class DataDictionaryExtensions
{
    /// <summary>
    /// Retrieves a typed value from the <see cref="DataDictionary"/>, deserializing if necessary.
    /// </summary>
    public static T? GetValue<T>(this DataDictionary extendedData, string key, JsonSerializerOptions options)
    {
        return extendedData.GetValue<T>(key, new SystemTextJsonSerializer(options));
    }

    /// <summary>
    /// Retrieves a typed value from the <see cref="DataDictionary"/>, deserializing if necessary.
    /// </summary>
    public static T? GetValue<T>(this DataDictionary extendedData, string key, ITextSerializer serializer)
    {
        if (!extendedData.TryGetValue(key, out object? data))
            throw new KeyNotFoundException($"Key \"{key}\" not found in the dictionary.");

        if (data is T value)
            return value;

        // JsonDocument -> JsonElement
        if (data is JsonDocument jsonDocument)
            data = jsonDocument.RootElement;

        if (data is JsonElement jsonElement)
        {
            try
            {
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
                return serializer.Deserialize<T>(elementJson);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                // Ignored - fall through to next handler.
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
                // Ignored - fall through to next handler.
            }
        }

        if (data is Dictionary<string, object?> dictionary)
        {
            try
            {
                object? normalizedDictionary = NormalizeValueForType(dictionary, typeof(T));
                string? dictJson = serializer.SerializeToString(normalizedDictionary);
                if (dictJson is not null)
                    return serializer.Deserialize<T>(dictJson);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                // Ignored - fall through to next handler.
            }
        }

        // List<object?> from ObjectToInferredTypesConverter (for array values)
        if (data is List<object?> list)
        {
            try
            {
                object? normalizedList = NormalizeValueForType(list, typeof(T));
                string? listJson = serializer.SerializeToString(normalizedList);
                if (listJson is not null)
                    return serializer.Deserialize<T>(listJson);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                // Ignored - fall through to next handler.
            }
        }

        // JSON string
        if (data is string json && json.IsJson())
        {
            try
            {
                return serializer.Deserialize<T>(json);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException)
            {
                // Ignored - fall through to direct type conversion.
            }
        }

        // Fallback: attempt direct type conversion
        try
        {
            if (data != null)
                return data.ToType<T>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or InvalidCastException or ArgumentException)
        {
            // Ignored - preserve legacy GetValue<T> behavior: failed conversion returns default.
        }

        return default;
    }

    private sealed record JsonPropertyBinding(string JsonName, Type PropertyType);

    private static readonly ConcurrentDictionary<Type, Dictionary<string, JsonPropertyBinding>> _propertyMaps = new();
    private static readonly JsonSerializerOptions _propertyNameOptions = new JsonSerializerOptions().ConfigureExceptionlessDefaults();

    private static object? NormalizeValueForType(object? value, Type targetType)
    {
        if (value is null)
            return null;

        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value is Dictionary<string, object?> dictionary)
            return NormalizeDictionaryForType(dictionary, targetType);

        if (value is List<object?> list)
        {
            Type? elementType = GetEnumerableElementType(targetType);
            if (elementType is null || elementType == typeof(object))
                return list;

            return list.Select(item => NormalizeValueForType(item, elementType)).ToList();
        }

        return value;
    }

    private static object NormalizeDictionaryForType(Dictionary<string, object?> dictionary, Type targetType)
    {
        if (ShouldPreserveDictionaryKeys(targetType))
            return dictionary;

        Dictionary<string, JsonPropertyBinding> propertyMap = _propertyMaps.GetOrAdd(targetType, CreatePropertyMap);
        if (propertyMap.Count == 0)
            return dictionary;

        var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var currentFormatKeys = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach ((string key, object? value) in dictionary)
        {
            if (!propertyMap.TryGetValue(key, out JsonPropertyBinding? property))
            {
                normalized[key] = value;
                continue;
            }

            object? normalizedValue = NormalizeValueForType(value, property.PropertyType);
            bool isCurrentFormatKey = String.Equals(key, property.JsonName, StringComparison.OrdinalIgnoreCase);

            if (normalized.TryGetValue(property.JsonName, out _) && currentFormatKeys.TryGetValue(property.JsonName, out bool existingIsCurrentFormatKey) && existingIsCurrentFormatKey && !isCurrentFormatKey)
                continue;

            normalized[property.JsonName] = normalizedValue;
            currentFormatKeys[property.JsonName] = isCurrentFormatKey;
        }

        return normalized;
    }

    private static Dictionary<string, JsonPropertyBinding> CreatePropertyMap(Type targetType)
    {
        var map = new Dictionary<string, JsonPropertyBinding>(StringComparer.OrdinalIgnoreCase);
        JsonTypeInfo typeInfo = _propertyNameOptions.GetTypeInfo(targetType);

        foreach (JsonPropertyInfo jsonProperty in typeInfo.Properties)
        {
            if (jsonProperty.AttributeProvider is not PropertyInfo property)
                continue;

            if (property.GetIndexParameters().Length > 0)
                continue;

            string jsonName = jsonProperty.Name;
            var binding = new JsonPropertyBinding(jsonName, property.PropertyType);

            map[jsonName] = binding;
            map[property.Name] = binding;
        }

        return map;
    }

    private static bool ShouldPreserveDictionaryKeys(Type targetType)
    {
        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (targetType == typeof(object) || targetType == typeof(DataDictionary))
            return true;

        if (typeof(IDictionary).IsAssignableFrom(targetType))
            return true;

        return targetType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
    }

    private static Type? GetEnumerableElementType(Type targetType)
    {
        if (targetType == typeof(string) || ShouldPreserveDictionaryKeys(targetType))
            return null;

        if (targetType.IsArray)
            return targetType.GetElementType();

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return targetType.GetGenericArguments()[0];

        return targetType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    public static void RemoveSensitiveData(this DataDictionary extendedData)
    {
        string[] removeKeys = [.. extendedData.Keys.Where(k => k.StartsWith('-'))];
        foreach (string key in removeKeys)
            extendedData.Remove(key);
    }
}
