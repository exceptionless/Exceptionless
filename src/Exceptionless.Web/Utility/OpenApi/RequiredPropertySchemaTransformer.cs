using System.Reflection;
using System.Runtime.CompilerServices;
using Exceptionless.Core.Extensions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Schema transformer that marks non-nullable properties as required in OpenAPI schemas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> Microsoft.AspNetCore.OpenApi doesn't consistently detect
/// required properties from C# nullability annotations. This causes all properties to
/// become optional in generated schemas, even when they're non-nullable in the C# model.
/// </para>
/// <para>
/// This transformer inspects C# nullability context and marks properties as required when:
/// <list type="bullet">
///   <item>The property type is a non-nullable value type (e.g., <c>int</c>, <c>bool</c>, <c>DateTime</c>)</item>
///   <item>The property type is a non-nullable reference type in a nullable-enabled context</item>
/// </list>
/// </para>
/// <para>
/// The <c>[Required]</c> attribute is also respected for explicit marking.
/// </para>
/// </remarks>
public class RequiredPropertySchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Properties is null || schema.Properties.Count == 0)
            return Task.CompletedTask;

        var type = context.JsonTypeInfo.Type;
        if (!type.IsClass && !type.IsValueType)
            return Task.CompletedTask;

        // Initialize Required collection if needed
        schema.Required ??= new HashSet<string>();

        // Get the nullability context for the type
        var nullabilityContext = new NullabilityInfoContext();
        var requiredProperties = new HashSet<string>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // Convert property name to snake_case to match schema
            string schemaPropertyName = property.Name.ToLowerUnderscoredWords();

            // Check if property exists in schema
            if (!schema.Properties.ContainsKey(schemaPropertyName))
                continue;

            // Skip properties that are already marked as required
            if (schema.Required.Contains(schemaPropertyName))
                continue;

            // Determine if property should be required
            if (IsPropertyRequired(property, nullabilityContext))
            {
                requiredProperties.Add(schemaPropertyName);
            }
        }

        // Add required properties to schema
        foreach (var propertyName in requiredProperties)
        {
            schema.Required.Add(propertyName);
        }

        return Task.CompletedTask;
    }

    private static bool IsPropertyRequired(PropertyInfo property, NullabilityInfoContext nullabilityContext)
    {
        var propertyType = property.PropertyType;

        // Non-nullable value types are always required (except when wrapped in Nullable<T>)
        if (propertyType.IsValueType)
        {
            // Nullable<T> is optional, plain value types are required
            return Nullable.GetUnderlyingType(propertyType) is null;
        }

        // For reference types, check nullability annotations
        try
        {
            var nullabilityInfo = nullabilityContext.Create(property);
            return nullabilityInfo.WriteState == NullabilityState.NotNull;
        }
        catch
        {
            // If we can't determine nullability, default to not required
            return false;
        }
    }
}
