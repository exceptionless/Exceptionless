using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> PropertyDescriptions = new();

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

            string? schemaName = SchemaReferenceIdHelper.CreateSchemaReferenceId(context.JsonTypeInfo);
            string? propertyName = JsonPropertyNameResolver.GetJsonPropertyName(context.JsonTypeInfo, property);
            if (schemaName is not null && propertyName is not null)
            {
                var descriptions = PropertyDescriptions.GetOrAdd(schemaName, _ => new());
                descriptions[propertyName] = description;
            }

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (!propertyType.IsEnum && propertySchema is OpenApiSchema mutableSchema && propertySchema is not OpenApiSchemaReference)
            {
                mutableSchema.Description = description;
            }
        }
    }

    internal static void ApplyReferencedPropertyDescriptions(OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
            return;

        var componentDescriptions = document.Components.Schemas
            .Where(pair => pair.Value is OpenApiSchema)
            .ToDictionary(pair => pair.Key, pair => ((OpenApiSchema)pair.Value).Description);

        foreach (var (schemaName, propertyDescriptions) in PropertyDescriptions)
        {
            if (!document.Components.Schemas.TryGetValue(schemaName, out var componentSchema) ||
                componentSchema is not OpenApiSchema schema || schema.Properties is null)
            {
                continue;
            }

            foreach (var (propertyName, description) in propertyDescriptions)
            {
                if (schema.Properties.TryGetValue(propertyName, out var propertySchema) &&
                    propertySchema is OpenApiSchemaReference referenceSchema)
                {
                    referenceSchema.Description = description;
                }
            }
        }

        foreach (var (schemaName, description) in componentDescriptions)
        {
            if (document.Components.Schemas.TryGetValue(schemaName, out var componentSchema) &&
                componentSchema is OpenApiSchema schema)
            {
                schema.Description = description;
            }
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

/// <summary>
/// Applies XML descriptions to referenced properties without moving them onto shared component schemas.
/// </summary>
public sealed class XmlDocumentationDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        XmlDocumentationSchemaTransformer.ApplyReferencedPropertyDescriptions(document);
        return Task.CompletedTask;
    }
}
