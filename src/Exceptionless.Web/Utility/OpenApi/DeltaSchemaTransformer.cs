using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Exceptionless.Core.Extensions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Schema transformer that populates Delta&lt;T&gt; schemas with the properties from T.
/// All properties are optional to represent PATCH semantics (partial updates).
/// </summary>
public class DeltaSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly NullabilityInfoContext NullabilityContext = new();

    public async Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        // Check if this is a Delta<T> type
        if (!IsDeltaType(type))
            return;

        // Get the inner type T from Delta<T>
        var innerType = type.GetGenericArguments().FirstOrDefault();
        if (innerType is null)
            return;

        // Set the type to object
        schema.Type = JsonSchemaType.Object;

        // Add properties from the inner type
        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        var unknownPropertiesProperty = type.GetProperty(nameof(Delta<object>.UnknownProperties));
        if (unknownPropertiesProperty is not null)
        {
            string? unknownPropertiesName = JsonPropertyNameResolver.GetJsonPropertyName(context.JsonTypeInfo, unknownPropertiesProperty);
            if (unknownPropertiesName is not null)
                schema.Properties.Remove(unknownPropertiesName);
        }

        foreach (var property in innerType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite))
        {
            bool isNullable = IsPropertyNullable(property);
            var generatedSchema = await context.GetOrCreateSchemaAsync(property.PropertyType, cancellationToken: cancellationToken);
            DataAnnotationHelper.ApplyToSchema(generatedSchema, property);
            ApplyArrayAnnotations(generatedSchema, property);

            OpenApiSchema propertySchema;

            if (isNullable && RequiresNullableWrapper(property.PropertyType))
            {
                propertySchema = new OpenApiSchema
                {
                    OneOf =
                    [
                        new OpenApiSchema { Type = JsonSchemaType.Null },
                        generatedSchema
                    ]
                };
            }
            else
            {
                propertySchema = generatedSchema;
                if (isNullable)
                    propertySchema.Type = propertySchema.Type.GetValueOrDefault() | JsonSchemaType.Null;
            }

            string propertyName = property.Name.ToLowerUnderscoredWords();
            schema.Properties[propertyName] = propertySchema;
        }

        // Ensure no required array - all properties are optional for PATCH
        schema.Required = null;
    }

    private static bool IsDeltaType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Delta<>);
    }

    private static bool IsPropertyNullable(PropertyInfo property)
    {
        // Check for Nullable<T> value types
        if (Nullable.GetUnderlyingType(property.PropertyType) is not null)
            return true;

        // Check for nullable reference types using NullabilityInfo
        try
        {
            var nullabilityInfo = NullabilityContext.Create(property);
            return nullabilityInfo.WriteState == NullabilityState.Nullable;
        }
        catch
        {
            // If we can't determine nullability, assume not nullable
            return false;
        }
    }

    private static bool RequiresNullableWrapper(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type != typeof(string) && !type.IsValueType;
    }

    private static void ApplyArrayAnnotations(OpenApiSchema schema, PropertyInfo property)
    {
        if (!schema.Type.HasValue || (schema.Type.Value & JsonSchemaType.Array) != JsonSchemaType.Array)
        {
            return;
        }

        var maxLength = property.GetCustomAttribute<MaxLengthAttribute>();
        if (maxLength is { Length: > -1 })
        {
            schema.MaxItems = maxLength.Length;
        }
    }
}
