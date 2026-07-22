using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Operation transformer that handles endpoints with [RequestBodyContent] attribute
/// to properly set the request body schema for raw content types.
/// </summary>
public class RequestBodyContentOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var methodInfo = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<MethodInfo>()
            .FirstOrDefault();

        if (methodInfo is null && context.Description.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerDescriptor)
        {
            // For controller actions, try to get from ControllerActionDescriptor
            methodInfo = controllerDescriptor.MethodInfo;
        }

        var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
        var multipartFileUploadAttribute = endpointMetadata.OfType<MultipartFileUploadAttribute>().FirstOrDefault()
            ?? methodInfo?.GetCustomAttributes(typeof(MultipartFileUploadAttribute), true).OfType<MultipartFileUploadAttribute>().FirstOrDefault();
        if (multipartFileUploadAttribute is not null)
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new()
                    {
                        Schema = CreateMultipartSchema(multipartFileUploadAttribute)
                    }
                }
            };

            return Task.CompletedTask;
        }

        var requestBodyContentAttribute = endpointMetadata.OfType<RequestBodyContentAttribute>().FirstOrDefault()
            ?? methodInfo?.GetCustomAttributes(typeof(RequestBodyContentAttribute), true).OfType<RequestBodyContentAttribute>().FirstOrDefault();
        if (requestBodyContentAttribute is null)
            return Task.CompletedTask;

        IEnumerable<string>? contentTypes = requestBodyContentAttribute.ContentTypes.Count > 0
            ? requestBodyContentAttribute.ContentTypes
            : (endpointMetadata.OfType<ConsumesAttribute>().FirstOrDefault()
                ?? methodInfo?.GetCustomAttributes(typeof(ConsumesAttribute), true).FirstOrDefault() as ConsumesAttribute)
            ?.ContentTypes.AsEnumerable()
            ?? operation.RequestBody?.Content?.Keys;
        if (contentTypes is null)
            return Task.CompletedTask;

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>()
        };

        foreach (string contentType in contentTypes)
        {
            operation.RequestBody.Content!.Add(contentType, new OpenApiMediaType
            {
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Example = JsonValue.Create(String.Empty) }
            });
        }

        return Task.CompletedTask;
    }

    private static OpenApiSchema CreateMultipartSchema(MultipartFileUploadAttribute attribute)
    {
        var required = new HashSet<string> { attribute.FileParameterName };
        var properties = new Dictionary<string, IOpenApiSchema>
        {
            [attribute.FileParameterName] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "binary",
                Description = attribute.FileDescription
            }
        };

        foreach (string parameterName in attribute.RequiredStringParameterNames)
        {
            required.Add(parameterName);
            properties[parameterName] = new OpenApiSchema { Type = JsonSchemaType.String };
        }

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = required,
            Properties = properties
        };
    }
}

/// <summary>
/// Attribute to mark endpoints that accept raw request body content.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class RequestBodyContentAttribute : Attribute
{
    public RequestBodyContentAttribute(params string[] contentTypes)
    {
        ContentTypes = contentTypes;
    }

    public IReadOnlyList<string> ContentTypes { get; }
}

/// <summary>
/// Attribute to mark endpoints that accept a multipart file upload.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class MultipartFileUploadAttribute : Attribute
{
    public string FileParameterName { get; }
    public string FileDescription { get; init; } = "The image file to upload.";
    public IReadOnlyCollection<string> RequiredStringParameterNames { get; }

    public MultipartFileUploadAttribute(string fileParameterName = "file", params string[] requiredStringParameterNames)
    {
        FileParameterName = fileParameterName;
        RequiredStringParameterNames = requiredStringParameterNames;
    }
}
