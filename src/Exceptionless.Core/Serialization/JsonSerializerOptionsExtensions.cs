using System.Text.Json;
using System.Text.Json.Serialization;

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
    /// <param name="options">The options to configure.</param>
    /// <returns>The configured options for chaining.</returns>
    public static JsonSerializerOptions ConfigureExceptionlessDefaults(this JsonSerializerOptions options)
    {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.PropertyNamingPolicy = LowerCaseUnderscoreNamingPolicy.Instance;
        options.Converters.Add(new ObjectToInferredTypesConverter());

        // Enforces C# nullable annotations (string vs string?) during serialization/deserialization.
        // If you see "cannot be null" errors, fix the model's nullability annotation or the data.
        options.RespectNullableAnnotations = true;

        return options;
    }
}
