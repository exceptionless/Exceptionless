using Exceptionless.Core.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

public sealed class EventEnvironmentSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!typeof(Event).IsAssignableFrom(context.JsonTypeInfo.Type) || schema.Properties is null)
            return Task.CompletedTask;

        var property = context.JsonTypeInfo.Type.GetProperty(nameof(Event.Environment))!;
        string? name = JsonPropertyNameResolver.GetJsonPropertyName(context.JsonTypeInfo, property);
        if (name is not null && schema.Properties.TryGetValue(name, out var propertySchema) && propertySchema is OpenApiSchema environment)
        {
            // The tolerant JSON converter accepts malformed input, but responses contain only a string or null.
            environment.Type = JsonSchemaType.String | JsonSchemaType.Null;
            environment.MaxLength = 64;
            schema.Required?.Remove(name);
        }

        return Task.CompletedTask;
    }
}
