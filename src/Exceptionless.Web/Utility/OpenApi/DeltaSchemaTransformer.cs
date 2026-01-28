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

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        // Check if this is a Delta<T> type
        if (!IsDeltaType(type))
            return Task.CompletedTask;

        // Get the inner type T from Delta<T>
        var innerType = type.GetGenericArguments().FirstOrDefault();
        if (innerType is null)
            return Task.CompletedTask;

        // Set the type to object
        schema.Type = JsonSchemaType.Object;

        // Add properties from the inner type
        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();

        foreach (var property in innerType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite)
                continue;

            bool isNullable = IsPropertyNullable(property);
            var propertySchema = CreateSchemaForType(property.PropertyType, isNullable);

            // Apply data annotations from the inner type's property
            DataAnnotationHelper.ApplyToSchema(propertySchema, property);

            string propertyName = property.Name.ToLowerUnderscoredWords();
            schema.Properties[propertyName] = propertySchema;
        }

        // Ensure no required array - all properties are optional for PATCH
        schema.Required = null;

        return Task.CompletedTask;
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

    private static OpenApiSchema CreateSchemaForType(Type type, bool isNullable)
    {
        var schema = new OpenApiSchema();
        JsonSchemaType schemaType = default;

        // Handle nullable value types (int?, DateTime?, etc.)
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType is not null)
        {
            type = underlyingType;
            isNullable = true;
        }

        // Add null type if nullable
        if (isNullable)
        {
            schemaType |= JsonSchemaType.Null;
        }

        if (type == typeof(string))
        {
            schemaType |= JsonSchemaType.String;
        }
        else if (type == typeof(bool))
        {
            schemaType |= JsonSchemaType.Boolean;
        }
        else if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
        {
            schemaType |= JsonSchemaType.Integer;
        }
        else if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            schemaType |= JsonSchemaType.Number;
        }
        else if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            schemaType |= JsonSchemaType.String;
            schema.Format = "date-time";
        }
        else if (type == typeof(Guid))
        {
            schemaType |= JsonSchemaType.String;
            schema.Format = "uuid";
        }
        else if (type.IsEnum)
        {
            schemaType |= JsonSchemaType.String;
        }
        else if (type.IsArray || (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)))
        {
            schemaType = JsonSchemaType.Array;
        }
        else
        {
            schemaType = JsonSchemaType.Object;
        }

        schema.Type = schemaType;
        return schema;
    }
}
