using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Metadata record that holds API documentation for an endpoint's parameters and responses.
/// Applied via .WithMetadata() on endpoint definitions.
/// </summary>
public sealed record EndpointDocumentation
{
    public Dictionary<string, string> ParameterDescriptions { get; init; } = new();
    public Dictionary<string, string> ResponseDescriptions { get; init; } = new();
}

/// <summary>
/// Operation transformer that reads EndpointDocumentation metadata
/// and applies parameter/response descriptions to the OpenAPI operation.
/// </summary>
public class EndpointDocumentationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var documentation = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<EndpointDocumentation>()
            .FirstOrDefault();

        if (documentation is null)
            return Task.CompletedTask;

        // Apply parameter descriptions
        if (operation.Parameters is not null)
        {
            foreach (var param in operation.Parameters)
            {
                if (param.Name is not null && documentation.ParameterDescriptions.TryGetValue(param.Name, out var description))
                {
                    param.Description = description;
                }
            }
        }

        // Apply response descriptions
        if (operation.Responses is not null)
        {
            foreach (var (code, desc) in documentation.ResponseDescriptions)
            {
                if (operation.Responses.TryGetValue(code, out var response))
                {
                    response.Description = desc;
                }
            }
        }

        return Task.CompletedTask;
    }
}
