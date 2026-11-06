using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

// The push middleware handles both SSE and WebSocket upgrades before endpoint routing.
public sealed class PushDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Paths["/api/v2/push"] = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = new OpenApiOperation
                {
                    OperationId = "GetPushStream",
                    Summary = "Subscribe to real-time notifications.",
                    Description = "Returns a long-lived server-sent event stream. Each data event contains a JSON object with type and message fields; comment frames keep the connection alive. Delivery is best-effort without replay: reconnecting clients should refetch current state. WebSocket upgrades remain supported for existing clients. Requires EnablePush (or the legacy EnableWebSockets setting).",
                    Security =
                    [
                        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] },
                        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Token", document)] = [] }
                    ],
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse
                        {
                            Description = "Server-sent event stream.",
                            Content = new Dictionary<string, OpenApiMediaType>
                            {
                                ["text/event-stream"] = new OpenApiMediaType
                                {
                                    Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                                }
                            }
                        },
                        ["401"] = new OpenApiResponse { Description = "Authentication is missing, invalid, or revoked." },
                        ["404"] = new OpenApiResponse { Description = "Push is disabled." },
                        ["429"] = new OpenApiResponse { Description = "The identity has reached its shared SSE and WebSocket connection limit." },
                        ["503"] = new OpenApiResponse { Description = "The connection lease store is unavailable." }
                    }
                }
            }
        };

        return Task.CompletedTask;
    }
}
