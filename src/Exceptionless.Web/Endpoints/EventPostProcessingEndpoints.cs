using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Services;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Endpoints;

public static class EventPostProcessingEndpoints
{
    public static void MapEventPostProcessing(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("api/v2/projects/{projectId:objectid}/events/posts/status", GetStatusesAsync)
            .WithName("GetEventPostProcessingStatusesV2")
            .ExcludeFromDescription()
            .WithTags("Event")
            .WithSummary("Get aggregate processing status for tracked V2 event posts.")
            .WithDescription("Returns terminal processing status for up to 1000 event-post identifiers returned in X-Exceptionless-Event-Post-Id headers.")
            .Accepts<EventPostProcessingStatusRequest>("application/json")
            .Produces<EventPostProcessingSummary>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(AuthorizationRoles.ClientPolicy)
            .AddOpenApiOperationTransformer((operation, context, _) =>
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", context.Document, null)] = []
                });
                return Task.CompletedTask;
            });
    }

    private static async Task<IResult> GetStatusesAsync(
        string projectId,
        EventPostProcessingStatusRequest request,
        HttpRequest httpRequest,
        EventPostService eventPostService,
        AppOptions options)
    {
        if (!options.EventIngestionV3.EnableProcessingStatus)
            return TypedResults.NotFound();

        string? claimProjectId = httpRequest.GetProjectId();
        if (claimProjectId is null || !String.Equals(projectId, claimProjectId, StringComparison.Ordinal))
            return TypedResults.NotFound();

        if (request.Ids is not { Count: >= 1 and <= 1000 })
            return InvalidIdentifiers("Between 1 and 1000 event-post identifiers are required.");

        string[] ids = request.Ids.Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Any(id => String.IsNullOrWhiteSpace(id) || id.Length > 256))
            return InvalidIdentifiers("Event-post identifiers must contain between 1 and 256 characters.");

        var statuses = await eventPostService.GetProcessingStatusesAsync(ids);
        int completed = statuses.Count(pair => String.Equals(pair.Value.ProjectId, projectId, StringComparison.Ordinal) && pair.Value.IsCompleted);
        int queued = statuses.Count(pair => String.Equals(pair.Value.ProjectId, projectId, StringComparison.Ordinal) && !pair.Value.IsCompleted);
        return TypedResults.Ok(new EventPostProcessingSummary(ids.Length, queued, completed, ids.Length - queued - completed));
    }

    private static IResult InvalidIdentifiers(string detail)
    {
        return TypedResults.Problem(
            detail,
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Invalid event-post identifiers");
    }
}
