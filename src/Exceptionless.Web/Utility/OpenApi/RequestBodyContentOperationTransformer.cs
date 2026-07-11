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
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Required = new HashSet<string> { multipartFileUploadAttribute.FileParameterName },
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                [multipartFileUploadAttribute.FileParameterName] = new OpenApiSchema
                                {
                                    Type = JsonSchemaType.String,
                                    Format = "binary",
                                    Description = "The image file to upload."
                                }
                            }
                        }
                    }
                }
            };

            return Task.CompletedTask;
        }

        bool hasRequestBodyContent = endpointMetadata.OfType<RequestBodyContentAttribute>().Any()
            || methodInfo?.GetCustomAttributes(typeof(RequestBodyContentAttribute), true).Any() == true;
        if (!hasRequestBodyContent)
            return Task.CompletedTask;

        var contentTypes = (endpointMetadata.OfType<ConsumesAttribute>().FirstOrDefault()
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
}

/// <summary>
/// Attribute to mark endpoints that accept raw request body content.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class RequestBodyContentAttribute : Attribute
{
}

/// <summary>
/// Attribute to mark endpoints that accept a multipart file upload.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class MultipartFileUploadAttribute : Attribute
{
    public string FileParameterName { get; }

    public MultipartFileUploadAttribute(string fileParameterName = "file")
    {
        FileParameterName = fileParameterName;
    }
}
