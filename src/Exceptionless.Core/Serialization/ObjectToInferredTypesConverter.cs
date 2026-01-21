using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Serialization;

/// <summary>
/// A high-performance System.Text.Json converter that deserializes <c>object</c>-typed properties
/// into appropriate .NET types instead of the default <see cref="JsonElement"/> behavior.
/// </summary>
/// <remarks>
/// <para>
/// By default, System.Text.Json deserializes properties typed as <c>object</c> into <see cref="JsonElement"/>,
/// which requires additional handling to extract values. This converter infers the actual type from the JSON
/// token and deserializes directly to native .NET types:
/// </para>
/// <list type="bullet">
///   <item><description><c>true</c>/<c>false</c> → <see cref="bool"/></description></item>
///   <item><description>Numbers → <see cref="long"/> (if fits) or <see cref="double"/></description></item>
///   <item><description>Strings with ISO 8601 date format → <see cref="DateTimeOffset"/></description></item>
///   <item><description>Other strings → <see cref="string"/></description></item>
///   <item><description><c>null</c> → <c>null</c></description></item>
///   <item><description>Objects → <see cref="Dictionary{TKey,TValue}"/> with <see cref="StringComparer.OrdinalIgnoreCase"/></description></item>
///   <item><description>Arrays → <see cref="List{T}"/> of <see cref="object"/></description></item>
/// </list>
/// <para>
/// This approach enables <c>GetValue&lt;T&gt;</c> in <see cref="DataDictionary"/> to work correctly
/// by re-serializing the dictionary and deserializing to the target type with proper naming policies.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var options = new JsonSerializerOptions
/// {
///     Converters = { new ObjectToInferredTypesConverter() }
/// };
/// 
/// // Deserializing { "count": 42, "name": "test" } into Dictionary&lt;string, object&gt;
/// // Results in: { "count": (long)42, "name": "test" } instead of JsonElement
/// </code>
/// </example>
/// <seealso href="https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to#deserialize-inferred-types-to-object-properties"/>
public sealed class ObjectToInferredTypesConverter : JsonConverter<object?>
{
    /// <inheritdoc />
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => ReadNumber(ref reader),
            JsonTokenType.String => ReadString(ref reader),
            JsonTokenType.Null => null,
            JsonTokenType.StartObject => ReadObject(ref reader, options),
            JsonTokenType.StartArray => ReadArray(ref reader, options),
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        // Handle JsonElement pass-through (may come from partial deserialization)
        if (value is JsonElement element)
        {
            element.WriteTo(writer);
            return;
        }

        // Serialize using the runtime type to get proper converter handling
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    /// <summary>
    /// Reads a JSON number, preferring <see cref="long"/> for integers and <see cref="double"/> for decimals.
    /// </summary>
    private static object ReadNumber(ref Utf8JsonReader reader)
    {
        // Prefer long for integer values (covers most IDs, counts, timestamps)
        if (reader.TryGetInt64(out long longValue))
            return longValue;

        // Fall back to double for floating-point
        return reader.GetDouble();
    }

    /// <summary>
    /// Reads a JSON string, attempting to parse as <see cref="DateTimeOffset"/> for ISO 8601 dates.
    /// </summary>
    private static object? ReadString(ref Utf8JsonReader reader)
    {
        // Attempt DateTimeOffset parsing for ISO 8601 formatted strings
        if (reader.TryGetDateTimeOffset(out DateTimeOffset dateTimeOffset))
            return dateTimeOffset;

        return reader.GetString();
    }

    /// <summary>
    /// Recursively reads a JSON object into a case-insensitive <see cref="Dictionary{TKey,TValue}"/>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="StringComparer.OrdinalIgnoreCase"/> for property name matching,
    /// consistent with <see cref="DataDictionary"/> behavior.
    /// </remarks>
    private static Dictionary<string, object?> ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return dictionary;

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            string propertyName = reader.GetString() ?? string.Empty;

            if (!reader.Read())
                continue;

            dictionary[propertyName] = ReadValue(ref reader, options);
        }

        return dictionary;
    }

    /// <summary>
    /// Recursively reads a JSON array into a <see cref="List{T}"/> of objects.
    /// </summary>
    private static List<object?> ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var list = new List<object?>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return list;

            list.Add(ReadValue(ref reader, options));
        }

        return list;
    }

    /// <summary>
    /// Reads a single JSON value of any type, dispatching to the appropriate reader method.
    /// </summary>
    private static object? ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => ReadNumber(ref reader),
            JsonTokenType.String => ReadString(ref reader),
            JsonTokenType.Null => null,
            JsonTokenType.StartObject => ReadObject(ref reader, options),
            JsonTokenType.StartArray => ReadArray(ref reader, options),
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };
    }
}
