using System.Text.Json;
using System.Text.Json.Nodes;

namespace Exceptionless.Tests.Utility;

public static class JsonNodeFormattingExtensions
{
    /// <summary>
    /// Converts a JsonNode to a pretty-printed JSON string for legacy snapshot assertions.
    /// </summary>
    public static string ToFormattedString(this JsonNode? node, JsonSerializerOptions options)
    {
        if (node is null)
            return "null";

        var clone = node.DeepClone();
        NormalizeDates(clone);

        return clone.ToJsonString(options);
    }

    private static void NormalizeDates(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var propertiesToUpdate = new List<(string key, string newValue)>();

            foreach (var prop in obj)
            {
                if (prop.Value is JsonValue val && val.GetValueKind() == JsonValueKind.String)
                {
                    string? strValue = val.GetValue<string>();
                    if (strValue is not null && IsIso8601DateWithZ(strValue))
                    {
                        string normalized = NormalizeDateString(strValue);
                        if (normalized != strValue)
                            propertiesToUpdate.Add((prop.Key, normalized));
                    }
                }
                else
                {
                    NormalizeDates(prop.Value);
                }
            }

            foreach (var (key, newValue) in propertiesToUpdate)
                obj[key] = JsonValue.Create(newValue);
        }
        else if (node is JsonArray arr)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                if (arr[i] is JsonValue val && val.GetValueKind() == JsonValueKind.String)
                {
                    string? strValue = val.GetValue<string>();
                    if (strValue is not null && IsIso8601DateWithZ(strValue))
                    {
                        string normalized = NormalizeDateString(strValue);
                        if (normalized != strValue)
                            arr[i] = JsonValue.Create(normalized);
                    }
                }
                else
                {
                    NormalizeDates(arr[i]);
                }
            }
        }
    }

    private static bool IsIso8601DateWithZ(string value)
    {
        return value.EndsWith('Z') &&
               value.Length >= 20 &&
               value.Length <= 28 &&
               value[4] == '-' &&
               value[7] == '-' &&
               value[10] == 'T' &&
               value[13] == ':' &&
               value[16] == ':';
    }

    private static string NormalizeDateString(string value)
    {
        if (DateTimeOffset.TryParse(value, out var date))
            return date.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz");

        return value;
    }
}
