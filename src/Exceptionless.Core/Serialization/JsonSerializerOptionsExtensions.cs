using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;
using Exceptionless.Core.Attributes;

namespace Exceptionless.Core.Serialization;

/// <summary>
/// Extension methods for configuring <see cref="JsonSerializerOptions"/> with Exceptionless conventions.
/// </summary>
public static class JsonSerializerOptionsExtensions
{
    /// <summary>
    /// Configures <see cref="JsonSerializerOptions"/> with Exceptionless app conventions:
    /// STJ snake_case property naming, null value handling, and dynamic object support.
    /// </summary>
    /// <remarks>
    /// These defaults intentionally differ from Foundatio's repository defaults. API and storage
    /// serialization omit nulls and empty collections by default, enforce nullable annotations for
    /// typed app models, and use Exceptionless' dynamic object inference. Elasticsearch source
    /// serialization layers repository defaults first, then applies the app-specific compatibility
    /// overrides it needs.
    /// </remarks>
    /// <param name="options">The options to configure.</param>
    /// <returns>The configured options for chaining.</returns>
    public static JsonSerializerOptions ConfigureExceptionlessDefaults(this JsonSerializerOptions options)
    {
        return ConfigureExceptionlessDefaults(options, skipEmptyCollections: true);
    }

    /// <summary>
    /// Configures API-specific response serialization on top of the shared naming and converter defaults.
    /// </summary>
    public static JsonSerializerOptions ConfigureExceptionlessApiDefaults(this JsonSerializerOptions options)
    {
        ConfigureExceptionlessDefaults(options, skipEmptyCollections: false);
        if (options.TypeInfoResolver is DefaultJsonTypeInfoResolver resolver)
        {
            resolver.Modifiers.Add(RemoveApiIgnoredProperties);
        }

        return options;
    }

    private static void RemoveApiIgnoredProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind is not JsonTypeInfoKind.Object)
        {
            return;
        }

        for (int index = typeInfo.Properties.Count - 1; index >= 0; index--)
        {
            if (typeInfo.Properties[index].AttributeProvider?.IsDefined(typeof(ApiIgnoreAttribute), inherit: true) is true)
            {
                typeInfo.Properties.RemoveAt(index);
            }
        }
    }

    private static JsonSerializerOptions ConfigureExceptionlessDefaults(JsonSerializerOptions options, bool skipEmptyCollections)
    {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.PropertyNameCaseInsensitive = true;

        // Allow non-ASCII Unicode (Chinese, Japanese, emoji, etc.) to pass through
        // unescaped for readability. HTML-sensitive characters (<, >, &, ') are still
        // escaped to their \uXXXX forms (e.g., & → \u0026); do not relax these escapes.
        var encoderSettings = new TextEncoderSettings(UnicodeRanges.All);
        encoderSettings.ForbidCharacter('<');
        encoderSettings.ForbidCharacter('>');
        encoderSettings.ForbidCharacter('&');
        encoderSettings.ForbidCharacter('\'');
        encoderSettings.ForbidCharacter('"');
        options.Encoder = JavaScriptEncoder.Create(encoderSettings);

        options.Converters.Add(new ObjectToInferredTypesConverter());

        // Required for public-field value types that are intentionally serialized through
        // the configured serializer, including ValueTuple cache keys and field-only structs.
        options.IncludeFields = true;

        // Enforces C# nullable annotations (string vs string?) for typed app models so bad
        // payload/model contracts fail close to the boundary instead of silently storing nulls.
        // Elasticsearch opts out below because historical documents may not match annotations.
        options.RespectNullableAnnotations = true;

        var resolver = new DefaultJsonTypeInfoResolver();
        if (skipEmptyCollections)
        {
            resolver.Modifiers.Add(EmptyCollectionModifier.SkipEmptyCollections);
        }

        options.TypeInfoResolver = resolver;
        return options;
    }
}
