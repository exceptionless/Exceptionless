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
        if (document.Paths is null)
        {
            return Task.CompletedTask;
        }

        foreach (var path in document.Paths)
        {
            if (path.Value?.Operations is null)
            {
                continue;
            }

            foreach (var operation in path.Value.Operations.Values)
            {
                if (operation?.Responses is null)
                {
                    continue;
                }

                foreach (var response in operation.Responses)
                {
                    // Only process 2xx success responses
                    if (response.Key.StartsWith('2') && response.Value?.Content is not null)
                    {
                        response.Value.Content.Remove(ProblemJsonContentType);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
