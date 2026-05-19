using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;

namespace Exceptionless.Core.Serialization;

/// <summary>
/// Extension methods for configuring <see cref="JsonSerializerOptions"/> with Exceptionless conventions.
/// </summary>
public static class JsonSerializerOptionsExtensions
{
    /// <summary>
    /// Configures <see cref="JsonSerializerOptions"/> with Exceptionless conventions:
    /// snake_case property naming, null value handling, and dynamic object support.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="JsonNamingPolicy.SnakeCaseLower"/> for property naming. Properties that
    /// have legacy field names requiring the old letter-by-letter convention (e.g.
    /// <c>OSName</c> → <c>o_s_name</c> instead of <c>os_name</c>) use
    /// <see cref="System.Text.Json.Serialization.JsonPropertyNameAttribute"/> overrides.
    /// </remarks>
    /// <param name="options">The options to configure.</param>
    /// <returns>The configured options for chaining.</returns>
    public static JsonSerializerOptions ConfigureExceptionlessDefaults(this JsonSerializerOptions options)
    {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.PropertyNameCaseInsensitive = true;

        // Allow non-ASCII Unicode (Chinese, Japanese, emoji, etc.) to pass through
        // unescaped for readability. Unlike the default encoder, this does NOT escape
        // '<', '>', '&' — that is intentional: those characters are safe in JSON responses
        // delivered with Content-Type: application/json, and over-escaping them breaks
        // user-visible strings. The JSON Content-Type header is the XSS boundary, not the encoder.
        options.Encoder = JavaScriptEncoder.Create(new TextEncoderSettings(UnicodeRanges.All));

        options.Converters.Add(new ObjectToInferredTypesConverter());

        // Ensures tuples and records are serialized with their field names instead of "Item1", "Item2", etc.
        options.IncludeFields = true;

        // Enforces C# nullable annotations (string vs string?) during serialization/deserialization.
        // If you see "cannot be null" errors, fix the model's nullability annotation or the data.
        options.RespectNullableAnnotations = true;

        // TypeInfoResolver + EmptyCollectionModifier omits empty lists/dicts from serialized
        // output (e.g. tags:[], references:[] are omitted). This applies to all API responses
        // and Elasticsearch documents, matching previous Newtonsoft behavior.
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { EmptyCollectionModifier.SkipEmptyCollections }
        };

        return options;
    }
}
