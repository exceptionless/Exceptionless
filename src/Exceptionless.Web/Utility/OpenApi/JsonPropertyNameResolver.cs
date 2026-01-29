using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Helper for resolving the effective JSON property name for CLR properties.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> When OpenAPI schema transformers need to look up schema properties
/// by CLR property, they must use the actual JSON property name that accounts for:
/// <list type="bullet">
///   <item><c>[JsonPropertyName]</c> attribute overrides</item>
///   <item>The active <c>JsonSerializerOptions.PropertyNamingPolicy</c></item>
/// </list>
/// </para>
/// <para>
/// Without this, transformers using hardcoded naming conventions (e.g., snake_case) will fail
/// to find properties on types like <c>ExternalAuthInfo</c> that use explicit
/// <c>[JsonPropertyName("camelCase")]</c> attributes.
/// </para>
/// </remarks>
public static class JsonPropertyNameResolver
{
    /// <summary>
    /// Gets the effective JSON property name for a CLR property by looking it up in the JsonTypeInfo.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type info from the OpenAPI transformer context.</param>
    /// <param name="clrProperty">The CLR property to look up.</param>
    /// <returns>The JSON property name, or null if not found.</returns>
    public static string? GetJsonPropertyName(JsonTypeInfo jsonTypeInfo, PropertyInfo clrProperty)
    {
        // JsonTypeInfo.Properties contains JsonPropertyInfo objects with the actual serialized names
        foreach (var jsonProperty in jsonTypeInfo.Properties)
        {
            // Match by the CLR property name (AttributeProvider is the PropertyInfo/FieldInfo)
            if (jsonProperty.AttributeProvider is PropertyInfo pi && pi.Name == clrProperty.Name)
            {
                return jsonProperty.Name;
            }
        }

        return null;
    }

    /// <summary>
    /// Tries to find the matching schema property for a CLR property.
    /// </summary>
    /// <param name="jsonTypeInfo">The JSON type info from the OpenAPI transformer context.</param>
    /// <param name="clrProperty">The CLR property to look up.</param>
    /// <param name="schemaProperties">The OpenAPI schema properties dictionary.</param>
    /// <param name="propertySchema">The matching schema property if found.</param>
    /// <returns>True if a matching schema property was found.</returns>
    public static bool TryGetSchemaProperty<TSchema>(
        JsonTypeInfo jsonTypeInfo,
        PropertyInfo clrProperty,
        IDictionary<string, TSchema> schemaProperties,
        out TSchema? propertySchema)
        where TSchema : class
    {
        propertySchema = null;

        var jsonPropertyName = GetJsonPropertyName(jsonTypeInfo, clrProperty);
        if (jsonPropertyName is null)
            return false;

        return schemaProperties.TryGetValue(jsonPropertyName, out propertySchema);
    }
}
