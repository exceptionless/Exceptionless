using System.Text.Json;

namespace Exceptionless.Core.Serialization;

public static class JsonElementConverter
{
    public static object? Convert(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => ConvertString(element),
            JsonValueKind.Number => JsonNumberInference.Convert(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray()
                .Select(Convert)
                .ToList(),
            JsonValueKind.Object => ConvertObject(element),
            _ => element.GetRawText()
        };
    }

    private static Dictionary<string, object?> ConvertObject(JsonElement element)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty prop in element.EnumerateObject())
            dict[prop.Name] = Convert(prop.Value);

        return dict;
    }

    private static object? ConvertString(JsonElement element)
    {
        // Use the decoded string value (not raw JSON text) so the 'T' check is consistent
        // with ObjectToInferredTypesConverter.ReadString which checks decoded bytes. This also
        // avoids allocating a second string for the non-date fallback.
        string? value = element.GetString();
        if (value is not null && value.Contains('T', StringComparison.Ordinal))
        {
            if (element.TryGetDateTimeOffset(out DateTimeOffset dateTimeOffset))
                return dateTimeOffset;

            if (element.TryGetDateTime(out DateTime dt))
                return dt;
        }

        return value;
    }
}
