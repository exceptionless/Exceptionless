using Foundatio.Repositories.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Document transformer that adds IAggregate schema and fixes CountResult.aggregations.
/// </summary>
public class AggregateDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        // First ensure IAggregate schema exists
        EnsureIAggregateSchema(document);

        // Remove any generic aggregate types that leaked through (they have backticks in names)
        RemoveGenericAggregateTypes(document);

        // Then fix CountResult.aggregations to reference IAggregate
        FixCountResultSchema(document);

        return Task.CompletedTask;
    }

    private static void EnsureIAggregateSchema(OpenApiDocument document)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        // Add IAggregate base schema if not present
        if (!document.Components.Schemas.ContainsKey("IAggregate"))
        {
            document.Components.Schemas["IAggregate"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = "Base interface for aggregation results. Concrete types include ValueAggregate, BucketAggregate, StatsAggregate, etc. See client-side type definitions for full type information.",
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["data"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        AdditionalProperties = new OpenApiSchema(),
                        Description = "Additional data associated with the aggregate."
                    }
                }
            };
        }
    }

    /// <summary>
    /// Removes generic aggregate types that have backticks in their names.
    /// These are C# generic types that don't translate well to OpenAPI.
    /// </summary>
    private static void RemoveGenericAggregateTypes(OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
            return;

        // Find and remove schemas with backticks (generic types) that implement IAggregate
        var schemasToRemove = document.Components.Schemas
            .Where(kvp => kvp.Key.Contains('`') || kvp.Key.Contains("Aggregate`"))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var schemaName in schemasToRemove)
        {
            document.Components.Schemas.Remove(schemaName);
        }
    }

    private static void FixCountResultSchema(OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
            return;

        // Find CountResult schema and fix aggregations property
        if (document.Components.Schemas.TryGetValue("CountResult", out var countResultSchema)
            && countResultSchema is OpenApiSchema schema
            && schema.Properties is not null)
        {
            if (schema.Properties.TryGetValue("aggregations", out var aggSchema) && aggSchema is OpenApiSchema aggregationsSchema)
            {
                // For OpenAPI 3.1, we need to use oneOf to properly represent nullable dictionaries
                // Clear the type array and use oneOf instead
                aggregationsSchema.Type = JsonSchemaType.Null; // Will be combined with the object schema
                aggregationsSchema.AdditionalProperties = null;

                // Replace with a proper nullable dictionary representation
                schema.Properties["aggregations"] = new OpenApiSchema
                {
                    OneOf = new List<IOpenApiSchema>
                    {
                        new OpenApiSchema { Type = JsonSchemaType.Null },
                        new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            AdditionalProperties = new OpenApiSchemaReference("IAggregate", document)
                        }
                    }
                };
            }
        }
    }
}
