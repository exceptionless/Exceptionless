using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Schema transformer that adds x-enumNames extension to enum schemas.
/// This enables swagger-typescript-api and similar generators to create
/// meaningful enum member names instead of Value0, Value1, etc.
/// </summary>
public class XEnumNamesSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (!type.IsEnum)
            return Task.CompletedTask;

        if (schema.Enum is null || schema.Enum.Count == 0)
            return Task.CompletedTask;

        string[] names = Enum.GetNames(type);
        var enumNamesArray = new JsonArray();

        foreach (string name in names)
            enumNamesArray.Add(name);

        schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        schema.Extensions["x-enumNames"] = new JsonNodeExtension(enumNamesArray);

        return Task.CompletedTask;
    }
}
