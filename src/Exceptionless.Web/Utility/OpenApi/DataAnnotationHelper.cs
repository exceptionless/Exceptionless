using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Exceptionless.Core.Attributes;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Helper for applying DataAnnotation attributes to OpenAPI schemas.
/// </summary>
/// <remarks>
/// <para>
/// This helper applies format/pattern annotations that ASP.NET Core's OpenAPI doesn't handle automatically.
/// The built-in OpenAPI support already handles common annotations like [Required], [StringLength], 
/// [MinLength], [MaxLength], and [Range], but does NOT handle format-related attributes.
/// </para>
/// <para>
/// <b>Supported annotations:</b>
/// <list type="bullet">
///   <item>[EmailAddress] → format: "email"</item>
///   <item>[Url] → format: "uri"</item>
///   <item>[ObjectId] → pattern for MongoDB ObjectId (24-char hex)</item>
/// </list>
/// </para>
/// <para>
/// To add support for additional annotations, add them here and they will automatically apply to:
/// <list type="bullet">
///   <item>Regular class/record properties via DataAnnotationsSchemaTransformer</item>
///   <item>Delta&lt;T&gt; PATCH models via DeltaSchemaTransformer</item>
/// </list>
/// </para>
/// </remarks>
public static class DataAnnotationHelper
{
    /// <summary>
    /// Applies DataAnnotation attributes to a string property schema.
    /// </summary>
    public static void ApplyToSchema(OpenApiSchema schema, PropertyInfo property)
    {
        if (!schema.Type.HasValue || (schema.Type.Value & JsonSchemaType.String) != JsonSchemaType.String)
            return;

        if (property.GetCustomAttribute<EmailAddressAttribute>() is not null)
        {
            schema.Format = "email";
        }
        else if (property.GetCustomAttribute<UrlAttribute>() is not null)
        {
            schema.Format = "uri";
        }
        else if (property.GetCustomAttribute<ObjectIdAttribute>() is not null)
        {
            schema.Pattern = ObjectIdAttribute.ObjectIdPattern;
            schema.MinLength = 24;
            schema.MaxLength = 24;
        }
        // Add additional format-related annotations here as needed.
        // Common candidates: [Phone] → format: "phone", [CreditCard] → pattern
    }
}
