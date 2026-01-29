using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Schema transformer that adds <c>readOnly: true</c> to properties that have only getters (no setters)
/// and removes <c>nullable: true</c> from get-only properties with field initializers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> Microsoft.AspNetCore.OpenApi marks get-only properties as nullable even when
/// they have initializers (e.g., <c>public List&lt;string&gt; Workers { get; } = new();</c>). SwashBuckle
/// correctly inferred that such properties are never null. This transformer restores that behavior.
/// </para>
/// <para>
/// <b>What it fixes:</b>
/// <list type="bullet">
///   <item><c>WorkInProgressResult.Workers</c> - get-only with <c>= new()</c> initializer</item>
///   <item>Any <c>ISet&lt;T&gt;</c> or <c>ICollection&lt;T&gt;</c> get-only properties with initializers</item>
/// </list>
/// </para>
/// <para>
/// The transformer detects initializers by looking for the compiler-generated backing field
/// that indicates the property has an inline initializer. It uses interface checks for broad
/// compatibility with different collection implementations.
/// </para>
/// <para>
/// This transformer resolves property names using the effective JSON property name
/// (respecting <c>[JsonPropertyName]</c> and the active naming policy) rather than
/// assuming a fixed naming convention.
/// </para>
/// </remarks>
public class ReadOnlyPropertySchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Properties is null || schema.Properties.Count == 0)
            return Task.CompletedTask;

        var type = context.JsonTypeInfo.Type;
        if (!type.IsClass)
            return Task.CompletedTask;

        foreach (var property in type.GetProperties()
            .Where(p => p.CanRead && !p.CanWrite))
        {
            // Use JsonTypeInfo to get the effective JSON property name (respects [JsonPropertyName] and naming policy)
            if (!JsonPropertyNameResolver.TryGetSchemaProperty(context.JsonTypeInfo, property, schema.Properties, out IOpenApiSchema? propertySchema) ||
                propertySchema is not OpenApiSchema mutableSchema)
                continue;

            // Mark as read-only since there's no setter
            mutableSchema.ReadOnly = true;

            // If the property has an initializer (backing field exists), it's never null
            // Remove nullable flag for properties with initializers
            if (HasBackingFieldWithInitializer(type, property) && mutableSchema.Type.HasValue)
            {
                // Remove the Null type flag to indicate the property is not nullable
                mutableSchema.Type = mutableSchema.Type.Value & ~JsonSchemaType.Null;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Detects if a get-only property has an inline field initializer.
    /// The C# compiler generates a backing field named <c>&lt;PropertyName&gt;k__BackingField</c>
    /// for auto-properties. If the property has an initializer, the backing field exists.
    /// </summary>
    private static bool HasBackingFieldWithInitializer(Type type, PropertyInfo property)
    {
        // Look for the compiler-generated backing field
        string backingFieldName = $"<{property.Name}>k__BackingField";
        var backingField = type.GetField(backingFieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        // If backing field exists and property type is a reference type that's non-nullable in the declaration,
        // it likely has an initializer. For collection types, this is almost always the case.
        if (backingField is null)
            return false;

        // For collection types, get-only with backing field almost always means initialized
        // Use interface checks for broader compatibility with different collection implementations
        var propertyType = property.PropertyType;

        // Check if the type implements common collection interfaces
        if (typeof(IEnumerable).IsAssignableFrom(propertyType) && propertyType != typeof(string))
        {
            // Check for generic collection interfaces
            if (propertyType.IsGenericType)
            {
                var genericTypeDef = propertyType.GetGenericTypeDefinition();

                // Check if it's a generic collection interface or implements one
                if (genericTypeDef == typeof(ICollection<>) ||
                    genericTypeDef == typeof(IList<>) ||
                    genericTypeDef == typeof(ISet<>) ||
                    genericTypeDef == typeof(IDictionary<,>) ||
                    genericTypeDef == typeof(IEnumerable<>) ||
                    genericTypeDef == typeof(IReadOnlyCollection<>) ||
                    genericTypeDef == typeof(IReadOnlyList<>) ||
                    genericTypeDef == typeof(IReadOnlySet<>) ||
                    genericTypeDef == typeof(IReadOnlyDictionary<,>))
                {
                    return true;
                }

                // Check if it implements any of these interfaces
                return propertyType.GetInterfaces()
                    .Where(iface => iface.IsGenericType)
                    .Any(iface => {
                        var ifaceGenericDef = iface.GetGenericTypeDefinition();
                        return ifaceGenericDef == typeof(ICollection<>) ||
                               ifaceGenericDef == typeof(IList<>) ||
                               ifaceGenericDef == typeof(ISet<>) ||
                               ifaceGenericDef == typeof(IDictionary<,>) ||
                               ifaceGenericDef == typeof(IReadOnlyCollection<>) ||
                               ifaceGenericDef == typeof(IReadOnlyList<>) ||
                               ifaceGenericDef == typeof(IReadOnlySet<>) ||
                               ifaceGenericDef == typeof(IReadOnlyDictionary<,>);
                    });
            }

            // Non-generic collections
            if (typeof(ICollection).IsAssignableFrom(propertyType) ||
                typeof(IList).IsAssignableFrom(propertyType) ||
                typeof(IDictionary).IsAssignableFrom(propertyType))
            {
                return true;
            }
        }

        return false;
    }
}
