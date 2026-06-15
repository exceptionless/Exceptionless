using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
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
        var requestBodySchema = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<RequestBodySchemaAttribute>()
            .FirstOrDefault();
        if (requestBodySchema is not null)
        {
            var document = context.Document!;
            document.Components ??= new OpenApiComponents();
            document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
            document.Components.Schemas.TryAdd(requestBodySchema.SchemaReferenceId, CreateJsonPatchSchema());

            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [requestBodySchema.ContentType] = new()
                    {
                        Schema = new OpenApiSchemaReference(requestBodySchema.SchemaReferenceId, document)
                    }
                }
            };

            return Task.CompletedTask;
        }

        var methodInfo = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<MethodInfo>()
            .FirstOrDefault();

        if (methodInfo is null && context.Description.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerDescriptor)
        {
            // For controller actions, try to get from ControllerActionDescriptor
            methodInfo = controllerDescriptor.MethodInfo;
        }

        if (methodInfo is null)
            return Task.CompletedTask;

        var multipartFileUploadAttribute = methodInfo.GetCustomAttributes(typeof(MultipartFileUploadAttribute), true).OfType<MultipartFileUploadAttribute>().FirstOrDefault();
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

        bool hasRequestBodyContent = methodInfo.GetCustomAttributes(typeof(RequestBodyContentAttribute), true).Any();
        if (!hasRequestBodyContent)
            return Task.CompletedTask;

        var consumesAttribute = methodInfo.GetCustomAttributes(typeof(ConsumesAttribute), true).FirstOrDefault() as ConsumesAttribute;
        if (consumesAttribute is null)
            return Task.CompletedTask;

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>()
        };

        foreach (string contentType in consumesAttribute.ContentTypes)
        {
            operation.RequestBody.Content!.Add(contentType, new OpenApiMediaType
            {
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Example = JsonValue.Create(String.Empty) }
            });
        }

        return Task.CompletedTask;
    }

    private static OpenApiSchema CreateJsonPatchSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Required = new HashSet<string> { "op", "path" },
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["op"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Enum = [JsonValue.Create("replace"), JsonValue.Create("test")],
                        Description = "The operation to perform (only 'replace' and 'test' are supported)."
                    },
                    ["path"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Description = "A JSON Pointer (RFC 6901) to the target property, using snake_case naming (e.g., '/full_name')."
                    },
                    ["value"] = new OpenApiSchema
                    {
                        Description = "The value to use for the operation."
                    },
                    ["from"] = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Description = "A JSON Pointer to the source property (only used with 'move' and 'copy' operations)."
                    }
                }
            }
        };
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

[AttributeUsage(AttributeTargets.Method)]
public class JsonPatchRequestBodyAttribute<T> : RequestBodySchemaAttribute
{
    public JsonPatchRequestBodyAttribute() : base($"{typeof(T).Name}JsonPatchDocument", "application/json-patch+json")
    {
    }
}

public abstract class RequestBodySchemaAttribute : Attribute
{
    protected RequestBodySchemaAttribute(string schemaReferenceId, string contentType)
    {
        SchemaReferenceId = schemaReferenceId;
        ContentType = contentType;
    }

    public string SchemaReferenceId { get; }
    public string ContentType { get; }
}
