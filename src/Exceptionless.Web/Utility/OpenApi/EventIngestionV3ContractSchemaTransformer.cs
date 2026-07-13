using Exceptionless.Core.Models.Ingestion;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Adds collection and dictionary value limits that data annotations cannot
/// express on the V3 wire model.
/// </summary>
public sealed class EventIngestionV3ContractSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Properties is null)
            return Task.CompletedTask;

        if (context.JsonTypeInfo.Type == typeof(EventIngestionV3Event)
            && JsonPropertyNameResolver.TryGetSchemaProperty(
                context.JsonTypeInfo,
                typeof(EventIngestionV3Event).GetProperty(nameof(EventIngestionV3Event.Tags))!,
                schema.Properties,
                out IOpenApiSchema? tagsProperty)
            && tagsProperty is OpenApiSchema tags)
        {
            tags.MaxItems = EventIngestionV3Limits.MaximumTags;
            if (tags.Items is OpenApiSchema tag)
            {
                tag.MinLength = 1;
                tag.MaxLength = EventIngestionV3Limits.MaximumTagLength;
            }
        }

        if (context.JsonTypeInfo.Type == typeof(EventIngestionV3Stacking)
            && JsonPropertyNameResolver.TryGetSchemaProperty(
                context.JsonTypeInfo,
                typeof(EventIngestionV3Stacking).GetProperty(nameof(EventIngestionV3Stacking.SignatureData))!,
                schema.Properties,
                out IOpenApiSchema? signatureProperty)
            && signatureProperty is OpenApiSchema signature)
        {
            signature.MinProperties = 1;
            signature.MaxProperties = EventIngestionV3Limits.MaximumMetadataEntries;
            if (signature.AdditionalProperties is OpenApiSchema value)
                value.MaxLength = EventIngestionV3Limits.MaximumMetadataValueLength;
        }

        return Task.CompletedTask;
    }
}
