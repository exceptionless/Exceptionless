using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Document transformer that removes application/problem+json content type from successful (2xx) responses.
/// The problem+json media type (RFC 7807) should only be used for error responses.
/// </summary>
public class RemoveProblemJsonFromSuccessResponsesTransformer : IOpenApiDocumentTransformer
{
    private const string ProblemJsonContentType = "application/problem+json";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (var operations in document.Paths.Select(p => p.Value.Operations))
        {
            if (operations is null)
                continue;

            foreach (var response in operations.Values.SelectMany(v => v.Responses ?? []).Where(r => r.Key.StartsWith('2') && r.Value.Content is not null))
            {
                // Only process 2xx success responses
                response.Value.Content?.Remove(ProblemJsonContentType);
            }
        }

        return Task.CompletedTask;
    }
}
