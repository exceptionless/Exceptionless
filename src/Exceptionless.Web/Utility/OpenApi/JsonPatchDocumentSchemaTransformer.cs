using System.Text.Json.Nodes;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Schema transformer that replaces auto-generated schemas for JsonPatchDocument&lt;T&gt; with the
/// standard RFC 6902 JSON Patch array schema: an array of operation objects with op, path, value, and from.
/// </summary>
public class JsonPatchDocumentSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!IsJsonPatchDocumentType(context.JsonTypeInfo.Type))
            return Task.CompletedTask;

        // RFC 6902: JSON Patch is an array of operations
        schema.Type = JsonSchemaType.Array;
        schema.Properties?.Clear();

        schema.Items = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string> { "op", "path" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["op"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [JsonValue.Create("replace"), JsonValue.Create("test")],
                    Description = "The operation to perform (only 'replace' and 'test' are supported)."
                },
                ["path"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Description = "A JSON Pointer (RFC 6901) to the target property, using snake_case naming (e.g., '/full_name')."
                },
                ["value"] = new OpenApiSchema
                {
                    Description = "The value to use for the operation."
                },
                ["from"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Description = "A JSON Pointer to the source property (only used with 'move' and 'copy' operations)."
                }
            }
        };

        return Task.CompletedTask;
    }

    private static bool IsJsonPatchDocumentType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(JsonPatchDocument<>);
    }
}
