using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Exceptionless.Core.Serialization;

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
        var dt = DateTime.Parse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        // Preserve MinValue semantics: normalize back to default if ticks are 0
        // This prevents Kind from changing during round-trip (Unspecified → UTC → Unspecified)
        if (dt == default)
            return default;

        return dt;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Preserve MinValue semantics: write without UTC conversion to avoid timezone shifts
        if (value == default)
        {
            writer.WriteStringValue(DateTime.MinValue.ToString("O", CultureInfo.InvariantCulture));
            return;
        }

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
