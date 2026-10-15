using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

internal static class ExceptionlessOpenApiServiceCollectionExtensions
{
    public const string DocumentName = "v1";

    public static IServiceCollection AddExceptionlessOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            // ASP.NET Core 11 defaults to OpenAPI 3.2; keep publishing 3.1 for existing client generators.
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
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
