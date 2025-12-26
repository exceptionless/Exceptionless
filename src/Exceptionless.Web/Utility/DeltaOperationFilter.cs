using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Web.Utility;

/// <summary>
/// Operation filter that unwraps Delta&lt;T&gt; types to expose the underlying T type in the OpenAPI schema.
/// This enables proper schema generation for PATCH endpoints that use Delta for partial updates.
/// </summary>
public class DeltaOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.RequestBody?.Content is null)
            return;

        foreach (var parameter in context.MethodInfo.GetParameters())
        {
            var parameterType = parameter.ParameterType;

            // Check if the parameter is Delta<T>
            if (parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(Delta<>))
            {
                var underlyingType = parameterType.GetGenericArguments()[0];

                // Generate schema for the underlying type
                var schema = context.SchemaGenerator.GenerateSchema(underlyingType, context.SchemaRepository);

                // Replace the Delta<T> schema with the underlying type's schema in all content types
                foreach (var content in operation.RequestBody.Content.Values)
                {
                    content.Schema = schema;
                }

                break; // Only one body parameter expected
            }
        }
    }
}
