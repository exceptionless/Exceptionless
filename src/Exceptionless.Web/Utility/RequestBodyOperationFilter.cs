using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

public class RequestBodyContentAttribute : Attribute
{
}

public class RequestBodyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        object? attributes = context.MethodInfo.GetCustomAttributes(typeof(RequestBodyContentAttribute), true).FirstOrDefault();
        if (attributes is null)
            return;

        var consumesAttribute = context.MethodInfo.GetCustomAttributes(typeof(ConsumesAttribute), true).FirstOrDefault() as ConsumesAttribute;
        if (consumesAttribute is null)
            return;

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
    }
}
