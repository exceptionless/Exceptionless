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
    /// Configures <see cref="JsonSerializerOptions"/> with Exceptionless conventions for WRITING:
    /// snake_case property naming, null value handling, and dynamic object support.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>IMPORTANT:</strong> These options include a <see cref="JsonNamingPolicy"/> that applies
    /// to BOTH serialization and deserialization. The options use PropertyNameCaseInsensitive
    /// to support matching both PascalCase and snake_case JSON property names.
    /// </para>
    /// <para>
    /// STJ's <see cref="JsonSerializerOptions.PropertyNamingPolicy"/> transforms C# property names
    /// before matching against JSON property names. For example, with our snake_case policy,
    /// <c>MachineName</c> becomes <c>machine_name</c>, which won't match a JSON property named
    /// <c>"MachineName"</c> even with <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/> enabled.
    /// </para>
    /// </remarks>
    /// <param name="options">The options to configure.</param>
    /// <returns>The configured options for chaining.</returns>
    public static JsonSerializerOptions ConfigureExceptionlessDefaults(this JsonSerializerOptions options)
    {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.PropertyNamingPolicy = LowerCaseUnderscoreNamingPolicy.Instance;
        options.PropertyNameCaseInsensitive = true;

        // XSS-safe encoder: escapes <, >, &, ' while allowing Unicode characters
        // This protects against script injection when JSON is embedded in HTML/JavaScript
        options.Encoder = JavaScriptEncoder.Create(new TextEncoderSettings(UnicodeRanges.All));

        options.Converters.Add(new ObjectToInferredTypesConverter());

        // Ensures tuples and records are serialized with their field names instead of "Item1", "Item2", etc.
        options.IncludeFields = true;

        // Enforces C# nullable annotations (string vs string?) during serialization/deserialization.
        // If you see "cannot be null" errors, fix the model's nullability annotation or the data.
        options.RespectNullableAnnotations = true;

        // Skip empty collections during serialization to match Newtonsoft behavior
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { EmptyCollectionModifier.SkipEmptyCollections }
        };

        return options;
    }
}
