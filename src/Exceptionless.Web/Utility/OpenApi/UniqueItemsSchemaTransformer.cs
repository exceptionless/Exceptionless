using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Schema transformer that adds uniqueItems: true to HashSet and ISet properties.
/// This maintains compatibility with the previous Swashbuckle-generated schema.
/// </summary>
public class UniqueItemsSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;

        // Check if this is a Set type (HashSet<T>, ISet<T>, etc.)
        if (IsSetType(type))
        {
            schema.UniqueItems = true;
        }

        return Task.CompletedTask;
    }

    private static bool IsSetType(Type type)
    {
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            if (genericTypeDef == typeof(HashSet<>) ||
                genericTypeDef == typeof(ISet<>) ||
                genericTypeDef == typeof(SortedSet<>))
            {
                return true;
            }
        }

        // Check if it implements ISet<T>
        return type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISet<>));
    }
}
