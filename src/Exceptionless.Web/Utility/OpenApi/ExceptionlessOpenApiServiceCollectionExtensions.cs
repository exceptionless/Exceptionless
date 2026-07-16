using Microsoft.Extensions.DependencyInjection;

namespace Exceptionless.Web.Utility.OpenApi;

internal static class ExceptionlessOpenApiServiceCollectionExtensions
{
    public const string DocumentName = "v1";

    public static IServiceCollection AddExceptionlessOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
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
            options.AddSchemaTransformer<NumericTypeSchemaTransformer>();
            options.AddSchemaTransformer<ReadOnlyPropertySchemaTransformer>();
            options.AddSchemaTransformer<RequiredPropertySchemaTransformer>();
            options.AddSchemaTransformer<UniqueItemsSchemaTransformer>();
            options.AddSchemaTransformer<XEnumNamesSchemaTransformer>();
        });

        return services;
    }
}
