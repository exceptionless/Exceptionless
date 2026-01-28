using Exceptionless.Core.Extensions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Schema transformer that fixes dictionary subclass properties (e.g., DataDictionary, SettingsDictionary)
/// to generate additionalProperties instead of opaque object types.
/// </summary>
public class DictionarySubclassSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Properties is null || schema.Properties.Count == 0)
            return Task.CompletedTask;

        var type = context.JsonTypeInfo.Type;
        if (!type.IsClass && !type.IsValueType)
            return Task.CompletedTask;

        foreach (var property in type.GetProperties())
        {
            var propertyType = property.PropertyType;

            // Unwrap Nullable<T> if applicable
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            // Check if property type inherits from Dictionary<,>
            if (!IsDictionarySubclass(underlyingType))
                continue;

            // Get dictionary key and value types
            var dictionaryTypes = GetDictionaryGenericArguments(underlyingType);
            if (dictionaryTypes is null)
                continue;

            // Only handle string-keyed dictionaries (JSON object keys must be strings)
            if (dictionaryTypes.Value.keyType != typeof(string))
                continue;

            // Find the matching schema property
            string schemaPropertyName = property.Name.ToLowerUnderscoredWords();
            if (!schema.Properties.TryGetValue(schemaPropertyName, out var propertySchema) || propertySchema is not OpenApiSchema mutableSchema)
                continue;

            // If it's an object type (possibly nullable) without additionalProperties, add them
            // Check that the type includes Object (it may also include Null for nullable types)
            bool isObjectType = mutableSchema.Type.HasValue && (mutableSchema.Type.Value & JsonSchemaType.Object) == JsonSchemaType.Object;
            if (isObjectType && mutableSchema.AdditionalProperties is null && (mutableSchema.Properties is null || mutableSchema.Properties.Count == 0))
            {
                // Create additionalProperties schema based on value type
                mutableSchema.AdditionalProperties = CreateSchemaForType(dictionaryTypes.Value.valueType);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks if a type is a subclass of Dictionary&lt;,&gt; (but not Dictionary itself).
    /// </summary>
    private static bool IsDictionarySubclass(Type type)
    {
        if (type.IsInterface || type.IsAbstract)
            return false;

        // Walk up the inheritance chain
        var current = type.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                return true;

            current = current.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Gets the generic type arguments (TKey, TValue) from a Dictionary subclass.
    /// </summary>
    private static (Type keyType, Type valueType)? GetDictionaryGenericArguments(Type type)
    {
        var current = type;
        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var args = current.GetGenericArguments();
                return (args[0], args[1]);
            }

            current = current.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Creates an OpenApiSchema for a given CLR type for use in additionalProperties.
    /// </summary>
    private static OpenApiSchema CreateSchemaForType(Type valueType)
    {
        // For object? (most common in DataDictionary), use empty schema (any type)
        if (valueType == typeof(object))
            return new OpenApiSchema();

        // For string, use string schema
        if (valueType == typeof(string))
            return new OpenApiSchema { Type = JsonSchemaType.String };

        // For other types, default to empty schema
        return new OpenApiSchema();
    }
}
