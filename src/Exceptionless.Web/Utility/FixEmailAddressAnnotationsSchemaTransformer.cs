using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace Exceptionless.Web.Utility;


// https://github.com/dotnet/aspnetcore/issues/58230#issuecomment-2569756615
//     public ICollection<OAuthAccount> OAuthAccounts { get; } = new Collection<OAuthAccount>();
// is marked as readonly
// https://github.com/microsoft/typespec/issues/3241#issuecomment-2570043836
// Odd https://github.com/dotnet/aspnetcore/issues/57390
public class FixEmailAddressAnnotationsSchemaTransformer : IOpenApiSchemaTransformer
{
    // [EmailAddress]
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!String.Equals(schema.Type, "string"))
            return Task.CompletedTask;

        bool hasEmailAddressAttribute = context.JsonPropertyInfo?.AttributeProvider?.GetCustomAttributes(typeof(EmailAddressAttribute), true) is { Length: 1 };
        if (hasEmailAddressAttribute)
        {
            schema.Format = "email";
        }

        return Task.CompletedTask;
    }
}
