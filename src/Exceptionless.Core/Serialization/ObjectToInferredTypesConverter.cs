using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Models;
using Newtonsoft.Json.Linq;

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
///   <item><description>Numbers → <see cref="int"/> (if fits), <see cref="long"/>, or <see cref="decimal"/>; with <c>preferInt64</c>, always <see cref="long"/> for integers and <see cref="double"/> for floats</description></item>
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
    private readonly bool _preferInt64;

    /// <summary>
    /// Initializes a new instance with default settings (integers that fit Int32 are returned as <see cref="int"/>).
    /// </summary>
    public ObjectToInferredTypesConverter() : this(preferInt64: false) { }

    /// <summary>
    /// Initializes a new instance with configurable integer handling.
    /// </summary>
    /// <param name="preferInt64">
    /// When <c>true</c>, all integers are returned as <see cref="long"/> to match JSON.NET behavior.
    /// Used by the Elasticsearch serializer to maintain compatibility with <c>DataObjectConverter</c>.
    /// </param>
    public ObjectToInferredTypesConverter(bool preferInt64)
    {
        _preferInt64 = preferInt64;
    }

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

        // Handle Newtonsoft JToken types (stored in DataDictionary by DataObjectConverter
        // when reading from Elasticsearch via NEST). Without this, STJ enumerates JToken's
        // IEnumerable<JToken> interface, producing nested empty arrays instead of proper JSON.
        if (value is JToken jToken)
        {
            using var doc = JsonDocument.Parse(jToken.ToString(Newtonsoft.Json.Formatting.None));
            doc.RootElement.WriteTo(writer);
            return;
        }

        // Serialize using the runtime type to get proper converter handling
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }

    /// <summary>
    /// Reads a JSON number, preserving the original representation (integer vs floating-point).
    /// </summary>
    /// <remarks>
    /// <para>This method preserves data integrity by checking the raw JSON text to determine
    /// if a number was written with a decimal point (e.g., <c>0.0</c>) vs as an integer (<c>0</c>).</para>
    /// <para>This is critical because:</para>
    /// <list type="bullet">
    ///   <item><description>User data must be preserved exactly as provided</description></item>
    ///   <item><description><c>TryGetInt64</c> would succeed for <c>0.0</c> since 0.0 == 0 mathematically</description></item>
    ///   <item><description>Serializing back would lose the decimal representation</description></item>
    /// </list>
    /// </remarks>
    private object ReadNumber(ref Utf8JsonReader reader)
    {
        // Check the raw text to preserve decimal vs integer representation
        // This is critical for data integrity - 0.0 should stay as double, not become 0L
        ReadOnlySpan<byte> rawValue = reader.HasValueSequence
            ? reader.ValueSequence.ToArray()
            : reader.ValueSpan;

        // If the raw text contains a decimal point or exponent, treat as floating-point
        if (rawValue.Contains((byte)'.') || rawValue.Contains((byte)'e') || rawValue.Contains((byte)'E'))
        {
            if (_preferInt64)
                return reader.GetDouble();

            return reader.GetDecimal();
        }

        // No decimal point - this is an integer
        if (_preferInt64)
        {
            // Match JSON.NET DataObjectConverter behavior: always return Int64
            if (reader.TryGetInt64(out long l))
                return l;
        }
        else
        {
            // Default STJ behavior: return smallest fitting integer type
            if (reader.TryGetInt32(out int i))
                return i;

            if (reader.TryGetInt64(out long l))
                return l;
        }

        // For very large integers that don't fit in long, fall back to decimal/double
        if (_preferInt64)
            return reader.GetDouble();

        return reader.GetDecimal();
    }

    /// <summary>
    /// Reads a JSON string, attempting to parse as <see cref="DateTimeOffset"/> for ISO 8601 dates.
    /// </summary>
    private static object? ReadString(ref Utf8JsonReader reader)
    {
        // Attempt DateTimeOffset parsing for ISO 8601 formatted strings
        if (reader.TryGetDateTimeOffset(out DateTimeOffset dateTimeOffset))
            return dateTimeOffset;

        if (reader.TryGetDateTime(out var dt))
            return dt;

        return reader.GetString();
    }

    /// <summary>
    /// Recursively reads a JSON object into a case-insensitive <see cref="Dictionary{TKey,TValue}"/>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="StringComparer.OrdinalIgnoreCase"/> for property name matching,
    /// consistent with <see cref="DataDictionary"/> behavior.
    /// </remarks>
    private Dictionary<string, object?> ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
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
    private List<object?> ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
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
    private object? ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
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

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to its native .NET type equivalent.
    /// Used by Event.OnDeserialized to avoid duplicating conversion logic.
    /// </summary>
    /// <remarks>
    /// This method provides the same type inference behavior as the main converter:
    /// objects → case-insensitive Dictionary, arrays → List, numbers → smallest fitting type, etc.
    /// </remarks>
    public static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => ConvertJsonElementString(element),
            JsonValueKind.Number => ConvertJsonElementNumber(element),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertJsonElement)
                .ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Converts a JsonElement number, preserving the original representation (integer vs floating-point).
    /// </summary>
    internal static object ConvertJsonElementNumber(JsonElement element)
    {
        // Check raw text for decimal point to preserve decimal vs integer representation
        string rawText = element.GetRawText();
        if (rawText.Contains('.') || rawText.Contains('e') || rawText.Contains('E'))
        {
            // Has decimal point or exponent - return decimal (default mode)
            return element.GetDecimal();
        }

        // No decimal point - integer. Try Int32 first, then Int64, then Decimal
        if (element.TryGetInt32(out int i))
            return i;

        if (element.TryGetInt64(out long l))
            return l;

        return element.GetDecimal();
    }

    /// <summary>
    /// Converts a JsonElement string, attempting DateTimeOffset parsing for ISO 8601 dates.
    /// </summary>
    internal static object? ConvertJsonElementString(JsonElement element)
    {
        if (element.TryGetDateTimeOffset(out DateTimeOffset dateTimeOffset))
            return dateTimeOffset;

        if (element.TryGetDateTime(out DateTime dt))
            return dt;

        return element.GetString();
    }
}
