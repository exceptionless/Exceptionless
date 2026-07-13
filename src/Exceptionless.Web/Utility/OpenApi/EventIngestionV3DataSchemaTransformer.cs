using System.Reflection;
using System.Text.Json;
using Exceptionless.Core.Models.Ingestion;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Advertises the V3 metadata bags that accept only JSON objects or null.
/// <see cref="EventIngestionV3Request.PostData"/> intentionally remains unconstrained JSON.
/// </summary>
public sealed class EventIngestionV3DataSchemaTransformer : IOpenApiSchemaTransformer
{
    private static readonly HashSet<Type> _objectDataTypes =
    [
        typeof(EventIngestionV3Event),
        typeof(EventIngestionV3User),
        typeof(EventIngestionV3Request),
        typeof(EventIngestionV3Environment)
    ];

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!_objectDataTypes.Contains(context.JsonTypeInfo.Type) || schema.Properties is null)
            return Task.CompletedTask;

        PropertyInfo? dataProperty = context.JsonTypeInfo.Type.GetProperty(nameof(EventIngestionV3Event.Data));
        if (dataProperty?.PropertyType != typeof(JsonElement?))
            return Task.CompletedTask;

        string? propertyName = JsonPropertyNameResolver.GetJsonPropertyName(context.JsonTypeInfo, dataProperty);
        if (propertyName is null || !schema.Properties.ContainsKey(propertyName))
            return Task.CompletedTask;

        // Nullable JsonElement properties are emitted as reference wrappers that are not
        // mutable OpenApiSchema instances. Replace the property schema at the parent.
        schema.Properties[propertyName] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object | JsonSchemaType.Null,
            AdditionalProperties = new OpenApiSchema()
        };
        return Task.CompletedTask;
    }
}
