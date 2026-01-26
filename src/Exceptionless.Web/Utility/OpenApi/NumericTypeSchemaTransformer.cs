using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Removes the "string" type from numeric schemas.
/// .NET's OpenAPI generator adds ["number", "string"] for JavaScript BigInt compatibility,
/// but this project prefers simple numeric types for TypeScript client generation.
/// </summary>
public class NumericTypeSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Type is null)
            return Task.CompletedTask;

        var schemaType = schema.Type.Value;

        // Check if this is a numeric type combined with string
        bool hasString = schemaType.HasFlag(JsonSchemaType.String);
        bool hasNumber = schemaType.HasFlag(JsonSchemaType.Number);
        bool hasInteger = schemaType.HasFlag(JsonSchemaType.Integer);
        bool hasNull = schemaType.HasFlag(JsonSchemaType.Null);

        // If it has (number or integer) AND string, remove the string
        if (hasString && (hasNumber || hasInteger))
        {
            // Remove the String flag, keep everything else
            schema.Type = schemaType & ~JsonSchemaType.String;

            // Remove the string-validation pattern since we're not accepting strings
            if (schema.Pattern is not null && schema.Pattern.StartsWith("^-?(?:0|"))
            {
                schema.Pattern = null;
            }
        }

        return Task.CompletedTask;
    }
}
