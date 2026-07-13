using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Document transformer that adds API information and security schemes to the OpenAPI document.
/// </summary>
public class DocumentInfoTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = context.DocumentName == "v3" ? "Exceptionless Event Ingestion API" : "Exceptionless API",
            Version = context.DocumentName,
            TermsOfService = new Uri("https://exceptionless.com/terms/"),
            Contact = new OpenApiContact
            {
                Name = "Exceptionless",
                Email = String.Empty,
                Url = new Uri("https://github.com/exceptionless/Exceptionless")
            },
            License = new OpenApiLicense
            {
                Name = "Apache License 2.0",
                Url = new Uri("https://github.com/exceptionless/Exceptionless/blob/main/LICENSE.txt")
            }
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Basic"] = new OpenApiSecurityScheme
            {
                Description = "Basic HTTP Authentication",
                Scheme = "basic",
                Type = SecuritySchemeType.Http
            },
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Description = "Authorization token. Example: \"Bearer {apikey}\"",
                Scheme = "bearer",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http
            },
            ["Token"] = new OpenApiSecurityScheme
            {
                Description = "Authorization token. Example: \"Bearer {apikey}\"",
                Name = "access_token",
                In = ParameterLocation.Query,
                Type = SecuritySchemeType.ApiKey
            }
        };

        document.Security ??= [];
        if (context.DocumentName == "v3")
        {
            document.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
            });
        }

        return Task.CompletedTask;
    }
}
