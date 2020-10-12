using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public class RequestBodyContentAttribute : Attribute { }

public class RequestBodyOperationFilter : IOperationFilter {
    public void Apply(OpenApiOperation operation, OperationFilterContext context) {
        var attributes = context.MethodInfo.GetCustomAttributes(typeof(RequestBodyContentAttribute), true).FirstOrDefault();
        if (attributes == null)
            return;

        var consumesAttribute = context.MethodInfo.GetCustomAttributes(typeof(ConsumesAttribute), true).FirstOrDefault() as ConsumesAttribute;
        if (consumesAttribute == null)
            return;

        operation.RequestBody = new OpenApiRequestBody { Required = true };
        foreach (string contentType in consumesAttribute.ContentTypes) {
            operation.RequestBody.Content.Add(contentType, new OpenApiMediaType {
                Schema = new OpenApiSchema { Type = "string", Example = new OpenApiString(String.Empty) }
            });
        }
    }
}