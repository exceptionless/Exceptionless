using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Services;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;

namespace Exceptionless.Web.Endpoints;

public static class EventIngestionV3Endpoints
{
    private const string ContentType = "application/x-ndjson";

    public static IEndpointRouteBuilder MapEventIngestionV3(this IEndpointRouteBuilder endpoints, AppOptions options)
    {
        var group = endpoints.MapGroup("/api/v3")
            .RequireAuthorization(AuthorizationRoles.ClientPolicy)
            .RequireRateLimiting("event-ingestion-v3")
            .WithTags("Event Ingestion V3");

        Map(group.MapPost("/events", HandleAsync), options)
            .WithName("PostEventsV3");
        Map(group.MapPost("/projects/{projectId:objectid}/events", HandleAsync), options)
            .WithName("PostEventsByProjectV3");

        return endpoints;
    }

    private static RouteHandlerBuilder Map(RouteHandlerBuilder builder, AppOptions options)
    {
        builder
            .Accepts<EventIngestionV3Event>(ContentType)
            .Produces<EventIngestionV3Response>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status413RequestEntityTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithRequestTimeout(new RequestTimeoutPolicy
            {
                Timeout = options.EventIngestionV3.RequestTimeout,
                TimeoutStatusCode = StatusCodes.Status503ServiceUnavailable
            });

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        string? projectId,
        IEventIngestionProcessor processor,
        IProjectRepository projectRepository,
        IOrganizationRepository organizationRepository,
        AppOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.EventIngestionV3.Enabled || options.EventSubmissionDisabled)
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Event ingestion is unavailable.");

        IHttpMaxRequestBodySizeFeature? requestSizeFeature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (requestSizeFeature is { IsReadOnly: false })
            requestSizeFeature.MaxRequestBodySize = options.EventIngestionV3.MaximumCompressedBodySize;
        if (request.ContentLength > options.EventIngestionV3.MaximumCompressedBodySize)
            return Results.Problem(statusCode: StatusCodes.Status413RequestEntityTooLarge, title: "The compressed request body is too large.");

        if (request.ContentType is null || !request.ContentType.StartsWith(ContentType, StringComparison.OrdinalIgnoreCase))
            return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: $"Content-Type must be {ContentType}.");

        string? contentEncoding = request.Headers.ContentEncoding.FirstOrDefault();
        if (!String.IsNullOrEmpty(contentEncoding)
            && !String.Equals(contentEncoding, "identity", StringComparison.OrdinalIgnoreCase)
            && !String.Equals(contentEncoding, "gzip", StringComparison.OrdinalIgnoreCase)
            && !String.Equals(contentEncoding, "br", StringComparison.OrdinalIgnoreCase))
            return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: "Content-Encoding must be gzip, br, or identity.");

        string? claimProjectId = request.GetProjectId();
        if (projectId is not null && claimProjectId is not null && !String.Equals(projectId, claimProjectId, StringComparison.Ordinal))
            return Results.NotFound();

        projectId ??= claimProjectId ?? request.GetDefaultProjectId();
        if (String.IsNullOrEmpty(projectId))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "No project was specified and no default project was found.");

        var project = await projectRepository.GetByIdAsync(projectId, o => o.Cache());
        if (project is null || !request.CanAccessOrganization(project.OrganizationId))
            return Results.NotFound();
        if (options.EventIngestionV3.AllowedProjectIds.Count > 0 && !options.EventIngestionV3.AllowedProjectIds.Contains(project.Id))
            return Results.NotFound();
        if (options.EventIngestionV3.AllowedOrganizationIds.Count > 0 && !options.EventIngestionV3.AllowedOrganizationIds.Contains(project.OrganizationId))
            return Results.NotFound();

        var organization = await organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
        if (organization is null)
            return Results.NotFound();
        if (organization.IsSuspended)
            return Results.Problem(statusCode: StatusCodes.Status402PaymentRequired, title: "The organization cannot accept events.");

        request.SetProject(project);
        var limitedBody = new EventPostRequestBodyStream(request.Body, options.EventIngestionV3.MaximumDecompressedBodySize);
        request.Body = limitedBody;

        var response = new EventIngestionV3Response();
        var batch = new List<EventIngestionV3Event>(options.EventIngestionV3.MicroBatchSize);
        long batchBytes = 0;
        int received = 0;

        using var activity = AppDiagnostics.StartActivity("Ingestion V3 Request");
        if (request.ContentLength.HasValue)
            AppDiagnostics.IngestionV3CompressedSize.Record(request.ContentLength.Value);
        AppDiagnostics.IngestionV3ActiveStreams.Add(1);
        try
        {
            await foreach (EventIngestionV3Event? sourceEvent in JsonSerializer.DeserializeAsyncEnumerable(
                request.BodyReader,
                EventIngestionJsonContext.Default.EventIngestionV3Event,
                topLevelValues: true,
                cancellationToken))
            {
                if (sourceEvent is null)
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The stream cannot contain null events.");

                received++;
                if (received > options.EventIngestionV3.MaximumEventsPerRequest)
                    return Results.Problem(statusCode: StatusCodes.Status413RequestEntityTooLarge, title: "The request contains too many events.");

                long eventSize = EventIngestionV3EventSizer.GetEstimatedSize(sourceEvent);
                if (eventSize > options.EventIngestionV3.MaximumEventSize || eventSize > options.EventIngestionV3.MaximumMicroBatchBytes)
                    return Results.Problem(statusCode: StatusCodes.Status413RequestEntityTooLarge, title: "An event exceeds the maximum event size.");
                if (batch.Count > 0 && batchBytes + eventSize > options.EventIngestionV3.MaximumMicroBatchBytes)
                {
                    response.Add(await processor.ProcessAsync(batch, organization, project, cancellationToken));
                    batch.Clear();
                    batchBytes = 0;
                }

                batch.Add(sourceEvent);
                batchBytes += eventSize;
                if (batch.Count < options.EventIngestionV3.MicroBatchSize)
                    continue;

                response.Add(await processor.ProcessAsync(batch, organization, project, cancellationToken));
                batch.Clear();
                batchBytes = 0;
            }

            if (limitedBody.RejectedStatusCode.HasValue)
            {
                return Results.Problem(statusCode: limitedBody.RejectedStatusCode, title: limitedBody.RejectionReason);
            }

            if (batch.Count > 0)
                response.Add(await processor.ProcessAsync(batch, organization, project, cancellationToken));
        }
        catch (JsonException) when (limitedBody.RejectedStatusCode.HasValue)
        {
            return Results.Problem(statusCode: limitedBody.RejectedStatusCode, title: limitedBody.RejectionReason);
        }
        catch (JsonException ex)
        {
            AppDiagnostics.IngestionV3Failures.Add(1);
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The event stream contains invalid JSON.", detail: ex.Message);
        }
        catch (RepositoryException)
        {
            AppDiagnostics.IngestionV3Failures.Add(1);
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Durable event storage is unavailable.");
        }
        catch (Exception)
        {
            AppDiagnostics.IngestionV3Failures.Add(1);
            throw;
        }
        finally
        {
            AppDiagnostics.IngestionV3DecompressedSize.Record(limitedBody.BytesRead);
            AppDiagnostics.IngestionV3ActiveStreams.Add(-1);
        }

        if (response.Received > 0 && response.Invalid == response.Received)
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "The stream did not contain any valid event records.");

        return Results.Json(response, EventIngestionJsonContext.Default.EventIngestionV3Response);
    }
}
