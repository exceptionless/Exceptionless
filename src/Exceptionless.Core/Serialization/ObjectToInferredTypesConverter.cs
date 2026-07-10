using System.Buffers;
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
/// <remarks>
/// This converter is app-specific and NOT interchangeable with Foundatio.Repositories'
/// ObjectToInferredTypesConverter. Key differences: preferInt64 mode for ES compatibility,
/// aggressive DateTimeOffset detection from strings, int→long→decimal number inference.
/// </remarks>
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
            JsonTokenType.Number => JsonNumberInference.Read(ref reader, _preferInt64),
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
    /// Reads a JSON string, attempting to parse as <see cref="DateTimeOffset"/> for ISO 8601 dates.
    /// </summary>
    /// <remarks>
    /// Only parses strings that contain the ISO 8601 time separator 'T'.
    /// Date-only strings like "2026-01-15" are preserved as strings to match the legacy
    /// Newtonsoft behavior (which used DateParseHandling.None for the Data dictionary).
    /// </remarks>
    private static object? ReadString(ref Utf8JsonReader reader)
    {
        // Check raw text for a time separator before attempting date parsing.
        // Date-only strings ("2026-01-15") should stay as strings — they may not represent
        // dates in user data. Only parse strings that have an explicit time component.
        ReadOnlySpan<byte> rawValue = reader.HasValueSequence
            ? reader.ValueSequence.ToArray()
            : reader.ValueSpan;

        bool hasTimeSeparator = rawValue.Contains((byte)'T');

        if (hasTimeSeparator)
        {
            if (reader.TryGetDateTimeOffset(out DateTimeOffset dateTimeOffset))
                return dateTimeOffset;

            if (reader.TryGetDateTime(out var dt))
                return dt;
        }

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
            JsonTokenType.Number => JsonNumberInference.Read(ref reader, _preferInt64),
            JsonTokenType.String => ReadString(ref reader),
            JsonTokenType.Null => null,
            JsonTokenType.StartObject => ReadObject(ref reader, options),
            JsonTokenType.StartArray => ReadArray(ref reader, options),
            _ => JsonDocument.ParseValue(ref reader).RootElement.Clone()
        };
    }

}
