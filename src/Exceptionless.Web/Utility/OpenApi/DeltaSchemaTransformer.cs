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
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        // Check if this is a Delta<T> type
        if (!IsDeltaType(type))
        {
            return Task.CompletedTask;
        }

        // Get the inner type T from Delta<T>
        var innerType = type.GetGenericArguments().FirstOrDefault();
        if (innerType is null)
        {
            return Task.CompletedTask;
        }

        // Set the type to object
        schema.Type = JsonSchemaType.Object;

        // Add properties from the inner type
        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();

        foreach (var property in innerType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite)
            {
                continue;
            }

            var propertySchema = CreateSchemaForType(property.PropertyType);
            var propertyName = property.Name.ToLowerUnderscoredWords();

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

    private static OpenApiSchema CreateSchemaForType(Type type)
    {
        var schema = new OpenApiSchema();
        JsonSchemaType schemaType = default;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType is not null)
        {
            type = underlyingType;
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
