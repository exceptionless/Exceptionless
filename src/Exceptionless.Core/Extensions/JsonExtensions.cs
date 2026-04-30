namespace Exceptionless.Core.Extensions;

/// <summary>
/// Extension methods for JSON operations using System.Text.Json.
/// For JsonNode/JsonObject operations, see <see cref="JsonNodeExtensions"/>.
/// </summary>
public static class JsonExtensions
{
    /// <summary>
    /// Checks if a string contains JSON content (starts with { or [).
    /// </summary>
    public static bool IsJson(this string value)
    {
        return value.GetJsonType() != JsonType.None;
    }

    /// <summary>
    /// Determines the JSON type of a string (Object, Array, or None).
    /// </summary>
    public static JsonType GetJsonType(this string value)
    {
        if (String.IsNullOrEmpty(value))
            return JsonType.None;

        for (int i = 0; i < value.Length; i++)
        {
            if (Char.IsWhiteSpace(value[i]))
                continue;

            if (value[i] == '{')
                return JsonType.Object;

            if (value[i] == '[')
                return JsonType.Array;

            break;
        }

        return JsonType.None;
    }


}

public enum JsonType : byte
{
    None,
    Object,
    Array
}
