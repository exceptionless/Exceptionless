using System.Reflection;
using Exceptionless.Core.Serialization;
using Exceptionless.Web.Api;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.OpenApi;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Exceptionless.Tests.Controllers;

internal static class MinimalApiTestApp
{
    public static WebApplication Create(bool useTestServer = false, bool includeOpenApi = false)
    {
        var builder = WebApplication.CreateBuilder();
        if (useTestServer)
            builder.WebHost.UseTestServer();

        builder.Services.AddAuthorization();
        builder.Services.AddAuthenticationCore();
        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.ConfigureExceptionlessApiDefaults();
        });
        builder.Services.AddRouting(options =>
        {
            options.ConstraintMap.Add("identifier", typeof(IdentifierRouteConstraint));
            options.ConstraintMap.Add("identifiers", typeof(IdentifiersRouteConstraint));
            options.ConstraintMap.Add("objectid", typeof(ObjectIdRouteConstraint));
            options.ConstraintMap.Add("objectids", typeof(ObjectIdsRouteConstraint));
            options.ConstraintMap.Add("token", typeof(TokenRouteConstraint));
            options.ConstraintMap.Add("tokens", typeof(TokensRouteConstraint));
        });
        builder.Services.AddSingleton<IServiceProviderIsService, PermissiveServiceProviderIsService>();
        builder.Services.AddSingleton<IMediatorResultMapper<Microsoft.AspNetCore.Http.IResult>, ApiResultMapper>();
        builder.Services.AddSingleton<IMediator>(_ => DispatchProxy.Create<IMediator, NullMediatorProxy>());

        if (includeOpenApi)
        {
            builder.Services.AddOpenApi(options =>
            {
                options.CreateSchemaReferenceId = SchemaReferenceIdHelper.CreateSchemaReferenceId;
                options.AddDocumentTransformer<AggregateDocumentTransformer>();
                options.AddDocumentTransformer<DocumentInfoTransformer>();
                options.AddDocumentTransformer<RemoveProblemJsonFromSuccessResponsesTransformer>();
                options.AddOperationTransformer<ObsoleteOperationTransformer>();
                options.AddOperationTransformer<RequestBodyContentOperationTransformer>();
                options.AddOperationTransformer<XmlDocumentationOperationTransformer>();
                options.AddOperationTransformer<EndpointDocumentationOperationTransformer>();
                options.AddSchemaTransformer<DataAnnotationsSchemaTransformer>();
                options.AddSchemaTransformer<DeltaSchemaTransformer>();
                options.AddSchemaTransformer<DictionarySubclassSchemaTransformer>();
                options.AddSchemaTransformer<NumericTypeSchemaTransformer>();
                options.AddSchemaTransformer<ReadOnlyPropertySchemaTransformer>();
                options.AddSchemaTransformer<RequiredPropertySchemaTransformer>();
                options.AddSchemaTransformer<UniqueItemsSchemaTransformer>();
                options.AddSchemaTransformer<XEnumNamesSchemaTransformer>();
            });
        }

        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        if (includeOpenApi)
            app.MapOpenApi("/docs/v2/openapi.json");

        app.MapApiEndpoints();
        return app;
    }

    private sealed class PermissiveServiceProviderIsService : IServiceProviderIsService
    {
        public bool IsService(Type serviceType)
        {
            var underlyingType = Nullable.GetUnderlyingType(serviceType) ?? serviceType;
            if (underlyingType == typeof(string) || underlyingType.IsPrimitive || underlyingType.IsEnum)
                return false;

            return true;
        }
    }

    private sealed class NullMediatorProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
                return null;

            return GetDefaultValue(targetMethod.ReturnType);
        }

        private static object? GetDefaultValue(Type type)
        {
            if (type == typeof(void))
                return null;

            if (type == typeof(Task))
                return Task.CompletedTask;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = type.GetGenericArguments()[0];
                var defaultValue = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
                return typeof(Task)
                    .GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(resultType)
                    .Invoke(null, [defaultValue]);
            }

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
