using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Web.Utility;

/// <summary>
/// Schema filter that adds x-enumNames extension to numeric enum schemas.
/// This enables swagger-typescript-api and similar generators to create
/// meaningful enum member names instead of Value0, Value1, etc.
/// </summary>
public class XEnumNamesSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concrete)
            return;

        var type = context.Type;
        if (type is null || !type.IsEnum)
            return;

        if (concrete.Enum is null || concrete.Enum.Count == 0)
            return;

        var names = Enum.GetNames(type);
        var enumNamesArray = new JsonArray();

        foreach (var name in names)
        {
            enumNamesArray.Add(name);
        }

        concrete.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        concrete.Extensions["x-enumNames"] = new JsonNodeExtension(enumNamesArray);
    }
}
