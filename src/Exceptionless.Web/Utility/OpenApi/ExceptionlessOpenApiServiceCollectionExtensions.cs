using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

internal static class ExceptionlessOpenApiServiceCollectionExtensions
{
    public static IServiceCollection AddExceptionlessOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v2", options =>
        {
            Configure(options);
            options.ShouldInclude = description => !IsV3Api(description.RelativePath);
        });
        services.AddOpenApi("v3", options =>
        {
            Configure(options);
            options.ShouldInclude = description => IsV3Api(description.RelativePath);
        });

        return services;
    }

    private static bool IsV3Api(string? relativePath) =>
        relativePath?.StartsWith("api/v3/", StringComparison.OrdinalIgnoreCase) is true;

    private static void Configure(OpenApiOptions options)
    {
        options.CreateSchemaReferenceId = SchemaReferenceIdHelper.CreateSchemaReferenceId;
        options.AddDocumentTransformer<AggregateDocumentTransformer>();
        options.AddDocumentTransformer<XmlDocumentationDocumentTransformer>();
        options.AddDocumentTransformer<DocumentInfoTransformer>();
        options.AddDocumentTransformer<RemoveProblemJsonFromSuccessResponsesTransformer>();
        options.AddOperationTransformer<ObsoleteOperationTransformer>();
        options.AddOperationTransformer<RequestBodyContentOperationTransformer>();
        options.AddOperationTransformer<XmlDocumentationOperationTransformer>();
        options.AddOperationTransformer<EndpointDocumentationOperationTransformer>();
        options.AddSchemaTransformer<DataAnnotationsSchemaTransformer>();
        options.AddSchemaTransformer<DeltaSchemaTransformer>();
        options.AddSchemaTransformer<XmlDocumentationSchemaTransformer>();
        options.AddSchemaTransformer<DictionarySubclassSchemaTransformer>();
        options.AddSchemaTransformer<EventIngestionV3ContractSchemaTransformer>();
        options.AddSchemaTransformer<EventIngestionV3DataSchemaTransformer>();
        options.AddSchemaTransformer<NumericTypeSchemaTransformer>();
        options.AddSchemaTransformer<ReadOnlyPropertySchemaTransformer>();
        options.AddSchemaTransformer<RequiredPropertySchemaTransformer>();
        options.AddSchemaTransformer<UniqueItemsSchemaTransformer>();
        options.AddSchemaTransformer<XEnumNamesSchemaTransformer>();
    }
}
