using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Defines an additional parameter to inject into the OpenAPI operation.
/// Used for parameters that are read from HttpContext rather than method signatures.
/// </summary>
public sealed record AdditionalParameterDefinition(
    string Name,
    string In, // "query" or "header"
    string? Description = null,
    bool Required = false,
    string Type = "string",
    string? Format = null
);

/// <summary>
/// Metadata record that holds API documentation for an endpoint's parameters and responses.
/// Applied via .WithMetadata() on endpoint definitions.
/// </summary>
public sealed record EndpointDocumentation
{
    public string? RequestBodyDescription { get; init; }
    public Dictionary<string, string> ParameterDescriptions { get; init; } = new();
    public Dictionary<string, string> ResponseDescriptions { get; init; } = new();
    public List<AdditionalParameterDefinition> AdditionalParameters { get; init; } = new();
}

/// <summary>
/// Operation transformer that reads EndpointDocumentation metadata
/// and applies parameter/response descriptions to the OpenAPI operation.
/// </summary>
public class EndpointDocumentationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var documentation = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<EndpointDocumentation>()
            .FirstOrDefault();

        if (documentation is null)
            return Task.CompletedTask;

        // Inject additional parameters that don't already exist
        if (documentation.AdditionalParameters.Count > 0)
        {
            operation.Parameters ??= [];

            foreach (var additionalParam in documentation.AdditionalParameters)
            {
                // Skip if parameter already exists
                var location = additionalParam.In == "header" ? ParameterLocation.Header : ParameterLocation.Query;
                if (operation.Parameters.Any(p => string.Equals(p.Name, additionalParam.Name, StringComparison.OrdinalIgnoreCase) && p.In == location))
                    continue;

                OpenApiSchema schema;

                if (additionalParam.Type == "array")
                {
                    // Array type — items are key-value pairs from query string
                    var itemSchema = new OpenApiSchema { Type = JsonSchemaType.Object };
                    itemSchema.Required = new HashSet<string> { "key", "value" };
                    itemSchema.Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["key"] = new OpenApiSchema { Type = JsonSchemaType.Null | JsonSchemaType.String },
                        ["value"] = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } }
                    };
                    schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Array,
                        Items = itemSchema
                    };
                }
                else
                {
                    schema = new OpenApiSchema();
                    schema.Type = additionalParam.Type switch
                    {
                        "integer" => JsonSchemaType.Integer,
                        "number" => JsonSchemaType.Number,
                        "boolean" => JsonSchemaType.Boolean,
                        _ => JsonSchemaType.String,
                    };
                    if (additionalParam.Format is not null)
                        schema.Format = additionalParam.Format;
                }

                var param = new OpenApiParameter
                {
                    Name = additionalParam.Name,
                    In = location,
                    Required = additionalParam.Required,
                    Schema = schema
                };

                if (additionalParam.Description is not null)
                    param.Description = additionalParam.Description;

                operation.Parameters.Add(param);
            }
        }

        // Apply parameter descriptions
        if (operation.Parameters is not null)
        {
            foreach (var param in operation.Parameters)
            {
                if (param.Name is not null && documentation.ParameterDescriptions.TryGetValue(param.Name, out var description))
                {
                    param.Description = description;
                }
            }
        }

        // Apply response descriptions
        if (operation.Responses is not null)
        {
            foreach (var (code, desc) in documentation.ResponseDescriptions)
            {
                if (operation.Responses.TryGetValue(code, out var response))
                {
                    response.Description = desc;
                }
            }
        }

        // Apply request body description
        if (documentation.RequestBodyDescription is not null && operation.RequestBody is not null)
        {
            operation.RequestBody.Description = documentation.RequestBodyDescription;
        }

        return Task.CompletedTask;
    }
}
