using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Restores XML type and property descriptions that were emitted by the previous OpenAPI generator.
/// </summary>
public sealed class XmlDocumentationSchemaTransformer : IOpenApiSchemaTransformer
{
    public async Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        var xmlDocumentation = await XmlDocumentationOperationTransformer.GetXmlDocumentationAsync(type.Assembly);
        if (xmlDocumentation is null)
            return;

        var typeElement = GetMember(xmlDocumentation, $"T:{GetTypeName(type)}");
        if (!type.IsEnum)
            schema.Description = GetSummary(typeElement) ?? schema.Description;

        if (schema.Properties is null)
            return;

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!JsonPropertyNameResolver.TryGetSchemaProperty(context.JsonTypeInfo, property, schema.Properties, out IOpenApiSchema? propertySchema))
                continue;

            var declaringType = property.DeclaringType ?? type;
            var propertyDocumentation = declaringType.Assembly == type.Assembly
                ? xmlDocumentation
                : await XmlDocumentationOperationTransformer.GetXmlDocumentationAsync(declaringType.Assembly);
            if (propertyDocumentation is null)
                continue;

            var propertyElement = GetMember(propertyDocumentation, $"P:{GetTypeName(declaringType)}.{property.Name}");
            string? description = GetSummary(propertyElement);
            if (description is null)
                continue;

            if (propertySchema is OpenApiSchema mutableSchema)
                mutableSchema.Description = description;
        }
    }

    private static XElement? GetMember(XDocument documentation, string memberName)
    {
        return documentation.Descendants("member")
            .FirstOrDefault(member => String.Equals(member.Attribute("name")?.Value, memberName, StringComparison.Ordinal));
    }

    private static string? GetSummary(XElement? member)
    {
        var summary = member?.Element("summary");
        if (summary is null)
            return null;

        string value = String.Concat(summary.Nodes().Select(GetText));
        var lines = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => String.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(line => line.Length > 0);
        string normalized = String.Join(Environment.NewLine, lines);
        return normalized.Length == 0 ? null : normalized;
    }

    private static string GetText(XNode node)
    {
        return node switch
        {
            XText text => text.Value,
            XElement element when element.Name.LocalName is "paramref" or "typeparamref" => element.Attribute("name")?.Value ?? String.Empty,
            XElement element when element.Name.LocalName is "see" => element.Attribute("langword")?.Value ?? GetCrefDisplayName(element.Attribute("cref")?.Value),
            XElement element => String.Concat(element.Nodes().Select(GetText)),
            _ => String.Empty
        };
    }

    private static string GetCrefDisplayName(string? cref)
    {
        if (String.IsNullOrEmpty(cref))
            return String.Empty;

        int separatorIndex = cref.LastIndexOf('.');
        return separatorIndex >= 0 ? cref[(separatorIndex + 1)..] : cref[(cref.IndexOf(':') + 1)..];
    }

    private static string GetTypeName(Type type)
    {
        var documentedType = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        return documentedType.FullName?.Replace('+', '.') ?? documentedType.Name;
    }
}
