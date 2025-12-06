using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Exceptionless.Web.Utility;

/// <summary>
/// Removes application/problem+json content type from successful (2xx) responses.
/// The problem+json media type (RFC 7807) should only be used for error responses.
/// </summary>
public class RemoveProblemJsonFromSuccessResponsesFilter : IDocumentFilter
{
    private const string ProblemJsonContentType = "application/problem+json";

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc.Paths is null)
            return;

        foreach (var path in swaggerDoc.Paths)
        {
            if (path.Value?.Operations is null)
                continue;

            foreach (var operation in path.Value.Operations.Values)
            {
                if (operation?.Responses is null)
                    continue;

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
    }
}
