using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Exceptionless.Web.Utility;

public class FixEnumsSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!context.JsonTypeInfo.Type.IsEnum)
            return Task.CompletedTask;

        var enumType = context.JsonTypeInfo.Type;
        bool isStringEnum = enumType.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType == typeof(JsonStringEnumConverter);

        schema.Enum.Clear();
        foreach (string name in Enum.GetNames(enumType))
        {
            if (isStringEnum)
            {
                string value = GetEnumName(enumType, name);
                schema.Enum.Add(new OpenApiString(value));
            }
            else
            {
                schema.Enum.Add(new OpenApiInteger((int)Enum.Parse(enumType, name)));
            }
        }

        // Add x-enumNames extension
        var enumNames = new OpenApiArray();
        enumNames.AddRange(Enum.GetNames(enumType).Select(name => new OpenApiString(name)));
        schema.Extensions["x-enumNames"] = enumNames;

        // Add enum schemas to OneOf
        foreach (string name in Enum.GetNames(enumType))
        {
            if (isStringEnum)
            {
                string value = GetEnumName(enumType, name);
                var enumSchema = new OpenApiSchema
                {
                    Type = "string", Enum = new List<IOpenApiAny> { new OpenApiString(value) }, Title = name
                };
                schema.OneOf.Add(enumSchema);
            }
            else
            {
                int enumValue = (int)Enum.Parse(enumType, name);
                var enumSchema = new OpenApiSchema
                {
                    Type = "integer", Enum = new List<IOpenApiAny> { new OpenApiInteger(enumValue) }, Title = name
                };

                schema.OneOf.Add(enumSchema);
            }
        }

        return Task.CompletedTask;
    }

    private static string GetEnumName(Type type, string name)
    {
        var memberInfo = type.GetMember(name).FirstOrDefault();
        var attribute = memberInfo?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();
        return attribute?.Name ?? throw new Exception("Enum member must have JsonStringEnumMemberNameAttribute");
    }
}
