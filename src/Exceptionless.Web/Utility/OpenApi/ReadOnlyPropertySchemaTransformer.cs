using Exceptionless.Core.Extensions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Schema transformer that adds readOnly: true to properties that have only getters (no setters).
/// This helps API consumers understand which properties are computed and cannot be set.
/// </summary>
public class ReadOnlyPropertySchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Properties is null || schema.Properties.Count == 0)
        {
            return Task.CompletedTask;
        }

        var type = context.JsonTypeInfo.Type;
        if (type is null || !type.IsClass)
        {
            return Task.CompletedTask;
        }

        foreach (var property in type.GetProperties())
        {
            if (!property.CanRead || property.CanWrite)
            {
                continue;
            }

            // Find the matching schema property (property names are in snake_case in the schema)
            var schemaPropertyName = property.Name.ToLowerUnderscoredWords();

            if (schema.Properties.TryGetValue(schemaPropertyName, out var propertySchema))
            {
                // Cast to OpenApiSchema to access mutable properties
                if (propertySchema is OpenApiSchema mutableSchema)
                {
                    mutableSchema.ReadOnly = true;
                }
            }
        }

        return Task.CompletedTask;
    }
}
