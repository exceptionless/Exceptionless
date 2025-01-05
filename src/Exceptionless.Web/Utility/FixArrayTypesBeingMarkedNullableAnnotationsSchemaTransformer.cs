using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace Exceptionless.Web.Utility;

public class FixArrayTypesBeingMarkedNullableAnnotationsSchemaTransformer : IOpenApiSchemaTransformer
{
    // public ICollection<string> OrganizationIds { get; } = new Collection<string>();
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!String.Equals(schema.Type, "array"))
            return Task.CompletedTask;

        if (context.JsonTypeInfo.IsReadOnly)
            schema.ReadOnly = context.JsonTypeInfo.IsReadOnly;

        if (context.JsonPropertyInfo is not null)
            schema.Nullable = context.JsonPropertyInfo.IsGetNullable;

        return Task.CompletedTask;
    }
}
