using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Exceptionless.Core.Attributes;
using Exceptionless.Core.Extensions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Schema transformer that applies DataAnnotation attributes to OpenAPI schemas.
/// Handles [EmailAddress], [Url], and [ObjectId] for both classes and records.
/// </summary>
public class DataAnnotationsSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Properties is null || schema.Properties.Count == 0)
            return Task.CompletedTask;

        var type = context.JsonTypeInfo.Type;
        if (!type.IsClass && !type.IsValueType)
            return Task.CompletedTask;

        foreach (var property in type.GetProperties())
        {
            var propertyType = property.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (underlyingType != typeof(string))
                continue;

            string schemaPropertyName = property.Name.ToLowerUnderscoredWords();
            if (!schema.Properties.TryGetValue(schemaPropertyName, out var propertySchema) || propertySchema is not OpenApiSchema mutableSchema)
                continue;

            // Get attributes from property or record constructor parameter
            var attributes = GetPropertyAttributes(type, property);

            if (HasAttribute<ObjectIdAttribute>(attributes))
            {
                mutableSchema.Pattern = ObjectIdAttribute.ObjectIdPattern;
            }
            else if (HasAttribute<EmailAddressAttribute>(attributes) && string.IsNullOrEmpty(mutableSchema.Format))
            {
                mutableSchema.Format = "email";
            }
            else if (HasAttribute<UrlAttribute>(attributes) && string.IsNullOrEmpty(mutableSchema.Format))
            {
                mutableSchema.Format = "uri";
            }
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<Attribute> GetPropertyAttributes(Type type, PropertyInfo property)
    {
        // First check property attributes
        foreach (var attr in property.GetCustomAttributes<Attribute>(inherit: true))
            yield return attr;

        // For records, also check constructor parameters
        var ctor = type.GetConstructors().FirstOrDefault(c => c.GetParameters().Length > 0);
        if (ctor is null)
            yield break;

        var param = ctor.GetParameters().FirstOrDefault(p =>
            string.Equals(p.Name, property.Name, StringComparison.OrdinalIgnoreCase));

        if (param is null)
            yield break;

        foreach (var attr in param.GetCustomAttributes<Attribute>(inherit: true))
            yield return attr;
    }

    private static bool HasAttribute<T>(IEnumerable<Attribute> attributes) where T : Attribute
        => attributes.OfType<T>().Any();
}
