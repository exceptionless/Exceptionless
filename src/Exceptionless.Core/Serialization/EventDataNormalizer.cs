using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Serialization;

internal static class EventDataNormalizer
{
    private static readonly HashSet<string> _mappedObjectDataKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        Event.KnownDataKeys.Error,
        Event.KnownDataKeys.EnvironmentInfo,
        Event.KnownDataKeys.Location,
        Event.KnownDataKeys.RequestInfo,
        Event.KnownDataKeys.SimpleError,
        Event.KnownDataKeys.SubmissionClient,
        Event.KnownDataKeys.UserDescription,
        Event.KnownDataKeys.UserInfo,
        Event.KnownDataKeys.ManualStackingInfo
    };

    public static void Normalize(DataDictionary? data)
    {
        if (data is null || data.Count is 0)
            return;

        foreach (string key in _mappedObjectDataKeys)
        {
            if (!data.TryGetValue(key, out object? value))
                continue;

            object? normalizedValue = NormalizeMappedObjectValue(key, value, out bool canUseMappedKey);
            if (canUseMappedKey)
            {
                if (!ReferenceEquals(value, normalizedValue))
                    data[key] = normalizedValue;
                continue;
            }

            data.Remove(key);
            data[GetEscapedKey(data, key)] = value;
        }
    }

    public static void Set(DataDictionary data, string key, object? value)
    {
        object? normalizedValue = NormalizeMappedObjectValue(key, value, out bool canUseMappedKey);
        data[canUseMappedKey ? GetAvailableKey(data, key) : GetEscapedKey(data, key)] = normalizedValue;
    }

    private static object? NormalizeMappedObjectValue(string key, object? value, out bool canUseMappedKey)
    {
        canUseMappedKey = true;

        if (!_mappedObjectDataKeys.Contains(key))
            return value;

        if (value is null || value is Dictionary<string, object?>)
            return value;

        if (value is string json && TryParseJsonObjectStringAsDictionary(json, out Dictionary<string, object?>? dictionary))
            return dictionary;

        canUseMappedKey = false;
        return value;
    }

    private static bool TryParseJsonObjectStringAsDictionary(string? json, out Dictionary<string, object?>? dictionary)
    {
        dictionary = null;

        if (String.IsNullOrWhiteSpace(json) || !json.IsJson())
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
                return false;

            if (JsonElementConverter.Convert(document.RootElement) is not Dictionary<string, object?> value)
                return false;

            dictionary = value;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static string GetAvailableKey(DataDictionary data, string key)
    {
        if (data.ContainsKey(key))
            key = key.StartsWith('@') ? "_" + key : key;

        int count = 1;
        string candidate = key;
        while (data.ContainsKey(candidate))
            candidate = key + count++;

        return candidate;
    }

    private static string GetEscapedKey(DataDictionary data, string key)
    {
        return GetAvailableKey(data, key.StartsWith('@') ? "_" + key : key);
    }
}
