using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elastic.Transport;

namespace Exceptionless.Core.Serialization;

/// <summary>
/// System.Text.Json serializer for the Elastic.Clients.Elasticsearch 8.x client.
///
/// This serializer extends <see cref="Serializer"/> to use System.Text.Json for document
/// serialization instead of the built-in serializer.
///
/// <para><strong>Why custom converters are needed:</strong></para>
/// <list type="bullet">
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
public sealed class ElasticSystemTextJsonSerializer : Serializer
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

        // Replace the default ObjectToInferredTypesConverter with one that returns Int64
        // for all integers, matching JSON.NET DataObjectConverter behavior. This ensures
        // Event.Data values round-trip through Elasticsearch with consistent types.
        var defaultConverter = options.Converters.FirstOrDefault(c => c is ObjectToInferredTypesConverter);
        if (defaultConverter is not null)
            options.Converters.Remove(defaultConverter);
        options.Converters.Insert(0, new ObjectToInferredTypesConverter(preferInt64: true));

        // Insert Elasticsearch converters for priority
        // Order matters: more specific converters should come first
        options.Converters.Insert(1, new Iso8601DateTimeOffsetConverter());
        options.Converters.Insert(2, new Iso8601DateTimeConverter());

        return options;
    }

    private JsonSerializerOptions GetOptions(SerializationFormatting formatting) =>
        formatting == SerializationFormatting.Indented ? _optionsIndented.Value : _optionsCompact.Value;

    #region Synchronous API

    /// <inheritdoc />
    public override object? Deserialize(Type type, Stream stream)
    {
        if (IsEmptyStream(stream))
            return null;

        // Fast path: MemoryStream with accessible buffer avoids buffering
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var segment))
            return JsonSerializer.Deserialize(segment.AsSpan((int)ms.Position), type, _optionsCompact.Value);

        return JsonSerializer.Deserialize(stream, type, _optionsCompact.Value);
    }

    /// <inheritdoc />
    public override T Deserialize<T>(Stream stream)
    {
        if (IsEmptyStream(stream))
            return default!;

        // Fast path: MemoryStream with accessible buffer avoids buffering
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var segment))
            return JsonSerializer.Deserialize<T>(segment.AsSpan((int)ms.Position), _optionsCompact.Value)!;

        return JsonSerializer.Deserialize<T>(stream, _optionsCompact.Value)!;
    }

    /// <inheritdoc />
    public override void Serialize<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None)
    {
        var options = GetOptions(formatting);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = formatting == SerializationFormatting.Indented,
            Encoder = options.Encoder
        });

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
    public override async ValueTask<object?> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
    {
        if (IsEmptyStream(stream))
            return null;

        return await JsonSerializer.DeserializeAsync(stream, type, _optionsCompact.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
    {
        if (IsEmptyStream(stream))
            return default!;

        var result = await JsonSerializer.DeserializeAsync<T>(stream, _optionsCompact.Value, cancellationToken)
            .ConfigureAwait(false);
        return result!;
    }

    /// <inheritdoc />
    public override void Serialize(object? data, Type type, Stream stream, SerializationFormatting formatting = SerializationFormatting.None, CancellationToken cancellationToken = default)
    {
        var options = GetOptions(formatting);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = formatting == SerializationFormatting.Indented,
            Encoder = options.Encoder
        });
        JsonSerializer.Serialize(writer, data, type, options);
    }

    /// <inheritdoc />
    public override Task SerializeAsync<T>(
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

    /// <inheritdoc />
    public override Task SerializeAsync(object? data, Type type, Stream stream, SerializationFormatting formatting = SerializationFormatting.None, CancellationToken cancellationToken = default)
    {
        var options = GetOptions(formatting);
        return JsonSerializer.SerializeAsync(stream, data, type, options, cancellationToken);
    }

    #endregion

    private static bool IsEmptyStream(Stream? stream)
    {
        return stream is null || stream == Stream.Null || (stream.CanSeek && stream.Length == 0);
    }
}

#region Elasticsearch-Specific Converters

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
