using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elasticsearch.Net;

namespace Exceptionless.Core.Serialization;

/// <summary>
/// System.Text.Json serializer for Elasticsearch NEST client.
///
/// This serializer implements <see cref="IElasticsearchSerializer"/> to enable the NEST 7.x
/// client to use System.Text.Json instead of Newtonsoft.Json for document serialization.
///
/// <para><strong>Why custom converters are needed:</strong></para>
/// <list type="bullet">
///   <item>
///     <term>DynamicDictionary</term>
///     <description>
///       Elasticsearch returns dynamic responses as <see cref="DynamicDictionary"/> which STJ
///       doesn't know how to serialize/deserialize. This converter handles the round-trip.
///     </description>
///   </item>
///   <item>
///     <term>DateTime/DateTimeOffset</term>
///     <description>
///       Elasticsearch expects ISO 8601 dates. STJ's default output uses "Z" suffix for UTC
///       while we need explicit "+00:00" for consistency with stored data.
///     </description>
///   </item>
/// </list>
///
/// <para><strong>Thread Safety:</strong> This class is thread-safe. Options are lazily initialized once.</para>
/// </summary>
public sealed class ElasticSystemTextJsonSerializer : IElasticsearchSerializer
{
    private readonly Lazy<JsonSerializerOptions> _optionsIndented;
    private readonly Lazy<JsonSerializerOptions> _optionsCompact;

    /// <summary>
    /// Creates a new serializer instance.
    /// </summary>
    /// <param name="baseOptions">
    /// Optional base options to extend. If provided, these options are cloned and augmented
    /// with Elasticsearch-specific converters. If null, default options are used.
    /// </param>
    public ElasticSystemTextJsonSerializer(JsonSerializerOptions? baseOptions = null)
    {
        _optionsIndented = new Lazy<JsonSerializerOptions>(() => CreateOptions(baseOptions, writeIndented: true));
        _optionsCompact = new Lazy<JsonSerializerOptions>(() => CreateOptions(baseOptions, writeIndented: false));
    }

    private static JsonSerializerOptions CreateOptions(JsonSerializerOptions? baseOptions, bool writeIndented)
    {
        var options = baseOptions is not null
            ? new JsonSerializerOptions(baseOptions)
            : new JsonSerializerOptions();

        // Elasticsearch convention: don't serialize null values
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.WriteIndented = writeIndented;

        // Insert Elasticsearch converters at the beginning for priority
        // Order matters: more specific converters should come first
        options.Converters.Insert(0, new DynamicDictionaryConverter());
        options.Converters.Insert(1, new Iso8601DateTimeOffsetConverter());
        options.Converters.Insert(2, new Iso8601DateTimeConverter());

        return options;
    }

    private JsonSerializerOptions GetOptions(SerializationFormatting formatting) =>
        formatting == SerializationFormatting.Indented ? _optionsIndented.Value : _optionsCompact.Value;

    #region Synchronous API

    /// <inheritdoc />
    public object? Deserialize(Type type, Stream stream)
    {
        if (IsEmptyStream(stream))
            return null;

        var buffer = ReadStreamToSpan(stream);
        return JsonSerializer.Deserialize(buffer, type, _optionsCompact.Value);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(Stream stream)
    {
        if (IsEmptyStream(stream))
            return default;

        var buffer = ReadStreamToSpan(stream);
        return JsonSerializer.Deserialize<T>(buffer, _optionsCompact.Value);
    }

    /// <inheritdoc />
    public void Serialize<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None)
    {
        using var writer = new Utf8JsonWriter(stream);
        var options = GetOptions(formatting);

        if (data is null)
        {
            JsonSerializer.Serialize(writer, (object?)null, typeof(object), options);
        }
        else
        {
            // Use runtime type to ensure proper polymorphic serialization
            JsonSerializer.Serialize(writer, data, data.GetType(), options);
        }
    }

    #endregion

    #region Asynchronous API

    /// <inheritdoc />
    public async Task<object?> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
    {
        if (IsEmptyStream(stream))
            return null;

        return await JsonSerializer.DeserializeAsync(stream, type, _optionsCompact.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        if (IsEmptyStream(stream))
            return default;

        return await JsonSerializer.DeserializeAsync<T>(stream, _optionsCompact.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task SerializeAsync<T>(
        T data,
        Stream stream,
        SerializationFormatting formatting = SerializationFormatting.None,
        CancellationToken cancellationToken = default)
    {
        var options = GetOptions(formatting);

        if (data is null)
        {
            return JsonSerializer.SerializeAsync(stream, (object?)null, typeof(object), options, cancellationToken);
        }

        return JsonSerializer.SerializeAsync(stream, data, data.GetType(), options, cancellationToken);
    }

    #endregion

    #region Stream Helpers

    private static bool IsEmptyStream(Stream? stream)
    {
        return stream is null || stream == Stream.Null || (stream.CanSeek && stream.Length == 0);
    }

    private static ReadOnlySpan<byte> ReadStreamToSpan(Stream stream)
    {
        // Fast path: if already a MemoryStream with accessible buffer, use it directly
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var segment))
        {
            return segment.AsSpan();
        }

        // Slow path: copy to new buffer
        using var buffer = stream.CanSeek
            ? new MemoryStream((int)stream.Length)
            : new MemoryStream();

        stream.CopyTo(buffer);
        return buffer.TryGetBuffer(out var seg) ? seg.AsSpan() : buffer.ToArray();
    }

    #endregion
}

#region Elasticsearch-Specific Converters

/// <summary>
/// Converts <see cref="DynamicDictionary"/> to/from JSON.
///
/// <para><strong>Why this converter exists:</strong></para>
/// Elasticsearch.Net uses <see cref="DynamicDictionary"/> for dynamic responses (e.g., script fields,
/// aggregation buckets). STJ has no built-in support for this type, so we must provide custom
/// serialization logic.
///
/// <para><strong>Serialization:</strong> Writes as a JSON object with key-value pairs.</para>
/// <para><strong>Deserialization:</strong> Reads JSON objects/arrays into DynamicDictionary.</para>
/// </summary>
internal sealed class DynamicDictionaryConverter : JsonConverter<DynamicDictionary>
{
    public override DynamicDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.StartArray => ReadFromArray(ref reader, options),
            JsonTokenType.StartObject => ReadFromObject(ref reader, options),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token type {reader.TokenType} when deserializing DynamicDictionary")
        };
    }

    private static DynamicDictionary ReadFromArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var array = JsonSerializer.Deserialize<object?[]>(ref reader, options);
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (array is not null)
        {
            for (var i = 0; i < array.Length; i++)
            {
                dict[i.ToString(CultureInfo.InvariantCulture)] = new DynamicValue(array[i]);
            }
        }

        return DynamicDictionary.Create(dict);
    }

    private static DynamicDictionary ReadFromObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(ref reader, options);
        return dict is not null ? DynamicDictionary.Create(dict!) : new DynamicDictionary();
    }

    public override void Write(Utf8JsonWriter writer, DynamicDictionary dictionary, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var (key, dynamicValue) in dictionary.GetKeyValues())
        {
            // Skip null values (consistent with DefaultIgnoreCondition.WhenWritingNull)
            if (dynamicValue?.Value is null)
                continue;

            writer.WritePropertyName(key);
            JsonSerializer.Serialize(writer, dynamicValue.Value, options);
        }

        writer.WriteEndObject();
    }
}

/// <summary>
/// Converts <see cref="DateTime"/> to/from ISO 8601 format for Elasticsearch.
///
/// <para><strong>Why this converter exists:</strong></para>
/// Elasticsearch indexes dates in ISO 8601 format. While STJ handles DateTime correctly,
/// this converter ensures consistent UTC conversion and format across the application.
///
/// <para><strong>Write behavior:</strong> Converts to UTC and outputs in round-trip format ("O").</para>
/// <para><strong>Read behavior:</strong> Parses ISO 8601 strings with culture-invariant settings.</para>
/// </summary>
internal sealed class Iso8601DateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        var dateString = reader.GetString();
        if (string.IsNullOrEmpty(dateString))
            return default;

        // Parse with DateTimeStyles to handle various ISO 8601 formats
        return DateTime.Parse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Always output in UTC with round-trip format for Elasticsearch compatibility
        var utcValue = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        writer.WriteStringValue(utcValue.ToString("O", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Converts <see cref="DateTimeOffset"/> to/from ISO 8601 format for Elasticsearch.
///
/// <para><strong>Why this converter exists:</strong></para>
/// DateTimeOffset preserves timezone offset information. This converter ensures the offset
/// is written in the explicit "+HH:mm" format (e.g., "+00:00") rather than "Z" for consistency
/// with historical data serialized by Newtonsoft.Json.
///
/// <para><strong>Write behavior:</strong> Outputs in round-trip format ("O") preserving offset.</para>
/// <para><strong>Read behavior:</strong> Parses ISO 8601 strings with culture-invariant settings.</para>
/// </summary>
internal sealed class Iso8601DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        var dateString = reader.GetString();
        if (string.IsNullOrEmpty(dateString))
            return default;

        return DateTimeOffset.Parse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        // Round-trip format preserves the exact offset (e.g., +00:00, -05:00)
        writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
    }
}

#endregion
