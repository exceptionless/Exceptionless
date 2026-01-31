using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Schema transformer that marks required properties in OpenAPI schemas based on C# required modifiers and value types.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> Microsoft.AspNetCore.OpenApi doesn't consistently detect
/// required properties from C# modifiers. This transformer ensures properties are marked as required
/// when appropriate for the OpenAPI schema.
/// </para>
/// <para>
/// This transformer marks properties as required when:
/// <list type="bullet">
///   <item>The property has the <c>required</c> modifier (C# 11+)</item>
///   <item>The property type is a non-nullable value type (e.g., <c>int</c>, <c>bool</c>, <c>DateTime</c>)</item>
/// </list>
/// </para>
/// <para>
/// Non-nullable reference types are NOT marked as required unless they have the explicit <c>required</c> modifier.
/// This correctly handles properties with default initializers (e.g., <c>public MyClass Prop { get; init; } = new();</c>).
/// </para>
/// <para>
/// This transformer resolves property names using the effective JSON property name
/// (respecting <c>[JsonPropertyName]</c> and the active naming policy) rather than
/// assuming a fixed naming convention.
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
            // Use JsonTypeInfo to get the effective JSON property name (respects [JsonPropertyName] and naming policy)
            var schemaPropertyName = JsonPropertyNameResolver.GetJsonPropertyName(context.JsonTypeInfo, property);
            if (schemaPropertyName is null)
                continue;

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

        // Check if the property is marked with the 'required' modifier (C# 11+)
        // This takes precedence over other heuristics
        var requiredMemberAttribute = property.GetCustomAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>();
        if (requiredMemberAttribute is not null)
            return true;

        // Non-nullable value types are always required (except when wrapped in Nullable<T>)
        if (propertyType.IsValueType)
        {
            // Nullable<T> is optional, plain value types are required
            return Nullable.GetUnderlyingType(propertyType) is null;
        }

        // For reference types with default initializers (e.g., "= new()"), we should NOT mark them as required
        // since they can be omitted during construction. However, we can't reliably detect initializers via reflection.
        // Instead, we only mark reference types as required if they have the 'required' modifier (checked above).
        // This means non-nullable reference types without 'required' are treated as optional.

        // For backwards compatibility and to match expected behavior, we do NOT mark non-nullable
        // reference types as required unless they have the explicit 'required' modifier.
        return false;
    }
}
