using System.IO.Compression;
using System.Text.Json;
using System.Threading.RateLimiting;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Serialization;
using Exceptionless.Core.Services;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Handlers;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Endpoints;

public static class EventIngestionV3Endpoints
{
    private const string ContentType = "application/x-ndjson";

    public static IEndpointRouteBuilder MapEventIngestionV3(this IEndpointRouteBuilder endpoints, AppOptions options)
    {
        var group = endpoints.MapGroup("/api/v3")
            .RequireAuthorization(AuthorizationRoles.ClientPolicy)
            .WithTags("Event Ingestion V3");

        Map(group.MapPost("/events", HandleDefaultProjectAsync), options)
            .WithName("PostEventsV3");
        Map(group.MapPost("/projects/{projectId:objectid}/events", HandleProjectAsync), options)
            .WithName("PostEventsByProjectV3");
        group.MapPost("/projects/{projectId:objectid}/events/processing/status", GetProcessingStatusAsync)
            .WithName("GetEventIngestionProcessingStatusV3")
            .ExcludeFromDescription()
            .WithSummary("Get full processing status for V3 events.")
            .WithDescription("Returns benchmark-oriented completion status for client event ids while terminal side-effect markers remain available.")
            .Accepts<EventIngestionV3ProcessingStatusRequest>("application/json")
            .Produces<EventIngestionV3ProcessingSummary>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .Produces(StatusCodes.Status404NotFound)
            .AddOpenApiOperationTransformer(AddBearerSecurityAsync);

        return endpoints;
    }

    private static async Task<IResult> GetProcessingStatusAsync(
        string projectId,
        EventIngestionV3ProcessingStatusRequest statusRequest,
        HttpRequest request,
        IEventIngestionIdStore eventIngestionIdStore,
        IngestionSideEffectExecutor sideEffectExecutor,
        AppOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.EventIngestionV3.EnableProcessingStatus)
        {
            return Results.NotFound();
        }

        string? claimProjectId = request.GetProjectId();
        if (claimProjectId is null || !String.Equals(projectId, claimProjectId, StringComparison.Ordinal))
        {
            return Results.NotFound();
        }

        if (statusRequest.ClientIds is not { Count: >= 1 and <= 1000 })
        {
            return InvalidProcessingIdentifiers("Between 1 and 1000 client event ids are required.");
        }

        string[] clientIds = statusRequest.ClientIds.Distinct(StringComparer.Ordinal).ToArray();
        if (clientIds.Any(id => String.IsNullOrWhiteSpace(id) || id.Length > EventIngestionV3Limits.MaximumEventIdLength))
        {
            return InvalidProcessingIdentifiers($"Client event ids must contain between 1 and {EventIngestionV3Limits.MaximumEventIdLength} characters.");
        }

        var assignedIds = await eventIngestionIdStore.GetAsync(projectId, clientIds, cancellationToken);
        string[] eventIds = assignedIds.Values
            .Select(identity => identity.EventId)
            .ToArray();
        var completed = await sideEffectExecutor.GetCompletedIdentitiesAsync(IngestionSideEffectExecutor.TerminalStage, projectId, eventIds);
        return Results.Ok(new EventIngestionV3ProcessingSummary(clientIds.Length, clientIds.Length - completed.Count, completed.Count));
    }

    private static IResult InvalidProcessingIdentifiers(string detail)
    {
        return Results.Problem(
            detail,
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Invalid client event ids");
    }

    private static RouteHandlerBuilder Map(RouteHandlerBuilder builder, AppOptions options)
    {
        builder
            .WithMetadata(EventIngestionV3EndpointMetadata.Instance)
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
            })
            .AddOpenApiOperationTransformer(AddBearerSecurityAsync);

        return builder;
    }

    private static Task AddBearerSecurityAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document, null)] = []
        });
        return Task.CompletedTask;
    }

    private static Task<IResult> HandleDefaultProjectAsync(
        HttpRequest request,
        EventIngestionV3Processor processor,
        EventIngestionV3ConcurrencyLimiter concurrencyLimiter,
        IProjectRepository projectRepository,
        IOrganizationRepository organizationRepository,
        AppOptions options,
        CancellationToken cancellationToken) =>
        HandleAsync(request, null, processor, concurrencyLimiter, projectRepository, organizationRepository, options, cancellationToken);

    private static Task<IResult> HandleProjectAsync(
        HttpRequest request,
        string projectId,
        EventIngestionV3Processor processor,
        EventIngestionV3ConcurrencyLimiter concurrencyLimiter,
        IProjectRepository projectRepository,
        IOrganizationRepository organizationRepository,
        AppOptions options,
        CancellationToken cancellationToken) =>
        HandleAsync(request, projectId, processor, concurrencyLimiter, projectRepository, organizationRepository, options, cancellationToken);

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        string? projectId,
        EventIngestionV3Processor processor,
        EventIngestionV3ConcurrencyLimiter concurrencyLimiter,
        IProjectRepository projectRepository,
        IOrganizationRepository organizationRepository,
        AppOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.EventIngestionV3.Enabled || options.EventSubmissionDisabled)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Event ingestion is unavailable.");
        }

        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out MediaTypeHeaderValue? mediaType)
            || !String.Equals(mediaType.MediaType.Value, ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: $"Content-Type must be {ContentType}.");
        }

        string[] contentEncodings = request.Headers.ContentEncoding
            .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
            .ToArray();
        if (contentEncodings.Length > 1)
        {
            return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: "Only one Content-Encoding may be specified.");
        }

        string? contentEncoding = contentEncodings.FirstOrDefault();
        if (!String.IsNullOrEmpty(contentEncoding)
            && !String.Equals(contentEncoding, "identity", StringComparison.OrdinalIgnoreCase)
            && !String.Equals(contentEncoding, "gzip", StringComparison.OrdinalIgnoreCase)
            && !String.Equals(contentEncoding, "br", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(statusCode: StatusCodes.Status415UnsupportedMediaType, title: "Content-Encoding must be gzip, br, or identity.");
        }

        string? claimProjectId = request.GetProjectId();
        if (projectId is not null && claimProjectId is not null && !String.Equals(projectId, claimProjectId, StringComparison.Ordinal))
        {
            return Results.NotFound();
        }

        projectId ??= claimProjectId ?? request.GetDefaultProjectId();
        if (String.IsNullOrEmpty(projectId))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "No project was specified and no default project was found.");
        }

        var project = await projectRepository.GetByIdAsync(projectId, o => o.Cache());
        if (project is null || !request.CanAccessOrganization(project.OrganizationId))
        {
            return Results.NotFound();
        }

        if (options.EventIngestionV3.AllowedProjectIds.Count > 0 && !options.EventIngestionV3.AllowedProjectIds.Contains(project.Id))
        {
            return Results.NotFound();
        }

        if (options.EventIngestionV3.AllowedOrganizationIds.Count > 0 && !options.EventIngestionV3.AllowedOrganizationIds.Contains(project.OrganizationId))
        {
            return Results.NotFound();
        }

        var organization = await organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
        if (organization is null)
        {
            return Results.NotFound();
        }

        if (organization.IsSuspended)
        {
            return Results.Problem(statusCode: StatusCodes.Status402PaymentRequired, title: "The organization cannot accept events.");
        }

        using RateLimitLease organizationStreamLease = await concurrencyLimiter.AcquireOrganizationActiveStreamAsync(organization.Id, cancellationToken);
        if (!organizationStreamLease.IsAcquired)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Event ingestion stream capacity is busy.");
        }

        request.SetProject(project);
        var compressedBodyState = request.HttpContext.Features.Get<EventIngestionV3RequestBodyState>();
        var limitedBody = new EventPostRequestBodyStream(
            request.Body,
            options.EventIngestionV3.MaximumDecompressedBodySize,
            "The decompressed request body is too large.",
            StatusCodes.Status400BadRequest,
            "The compressed request body is invalid.");
        request.Body = limitedBody;

        var response = new EventIngestionV3Response();
        var batch = new List<EventIngestionV3BufferedRecord>(options.EventIngestionV3.MicroBatchSize);
        long batchBytes = 0;
        int received = 0;
        long maximumRecordSize = Math.Min(options.EventIngestionV3.MaximumEventSize, options.EventIngestionV3.MaximumMicroBatchBytes);

        using var activity = AppDiagnostics.StartActivity("Ingestion V3 Request");
        if (request.ContentLength.HasValue)
        {
            AppDiagnostics.IngestionV3CompressedSize.Record(request.ContentLength.Value);
        }

        AppDiagnostics.IngestionV3ActiveStreams.Add(1);
        try
        {
            while (await EventIngestionV3StreamReader.ReadAsync(request.BodyReader, maximumRecordSize, cancellationToken) is { } record)
            {
                EventIngestionV3BufferedRecord bufferedRecord = record.BufferedRecord;
                bool addedToBatch = false;
                try
                {
                    received++;
                    if (received > options.EventIngestionV3.MaximumEventsPerRequest)
                    {
                        return Problem(response, StatusCodes.Status413RequestEntityTooLarge, "The request contains too many events.");
                    }

                    long eventSize = record.Size;
                    if (batch.Count > 0 && batchBytes + eventSize > options.EventIngestionV3.MaximumMicroBatchBytes)
                    {
                        response.Add(await ProcessBatchAsync(processor, concurrencyLimiter, batch, organization, project, cancellationToken));
                        batchBytes = 0;
                    }

                    batch.Add(bufferedRecord);
                    addedToBatch = true;
                    batchBytes += eventSize;
                    if (batch.Count < options.EventIngestionV3.MicroBatchSize)
                    {
                        continue;
                    }

                    response.Add(await ProcessBatchAsync(processor, concurrencyLimiter, batch, organization, project, cancellationToken));
                    batchBytes = 0;
                }
                finally
                {
                    if (!addedToBatch)
                    {
                        bufferedRecord.Dispose();
                    }
                }
            }

            if (GetBodyRejection(limitedBody, compressedBodyState) is { } rejection)
            {
                return Problem(response, rejection.StatusCode, rejection.Reason);
            }

            if (batch.Count > 0)
            {
                response.Add(await ProcessBatchAsync(processor, concurrencyLimiter, batch, organization, project, cancellationToken));
            }
        }
        catch (EventIngestionV3RecordTooLargeException)
        {
            return Problem(response, StatusCodes.Status413RequestEntityTooLarge, "An event exceeds the maximum event size.");
        }
        catch (JsonException) when (GetBodyRejection(limitedBody, compressedBodyState) is not null)
        {
            BodyRejection rejection = GetBodyRejection(limitedBody, compressedBodyState)!;
            return Problem(response, rejection.StatusCode, rejection.Reason);
        }
        catch (JsonException ex)
        {
            AppDiagnostics.IngestionV3Failures.Add(1);
            return Problem(response, StatusCodes.Status400BadRequest, "The event stream contains invalid JSON.", ex.Message);
        }
        catch (InvalidDataException) when (GetBodyRejection(limitedBody, compressedBodyState) is not null)
        {
            BodyRejection rejection = GetBodyRejection(limitedBody, compressedBodyState)!;
            return Problem(response, rejection.StatusCode, rejection.Reason);
        }
        catch (InvalidDataException ex)
        {
            AppDiagnostics.IngestionV3Failures.Add(1);
            return Problem(response, StatusCodes.Status400BadRequest, "The compressed request body is invalid.", ex.Message);
        }
        catch (ProcessingConcurrencyRejectedException)
        {
            return Problem(response, StatusCodes.Status429TooManyRequests, "Event ingestion processing capacity is busy.");
        }
        catch (EventBatchWriteException)
        {
            AppDiagnostics.IngestionV3Failures.Add(1);
            return Problem(response, StatusCodes.Status503ServiceUnavailable, "Durable event processing is unavailable.");
        }
        catch (RepositoryException)
        {
            AppDiagnostics.IngestionV3Failures.Add(1);
            return Problem(response, StatusCodes.Status503ServiceUnavailable, "Durable event storage is unavailable.");
        }
        catch (Exception)
        {
            AppDiagnostics.IngestionV3Failures.Add(1);
            throw;
        }
        finally
        {
            DisposeBatch(batch);
            AppDiagnostics.IngestionV3DecompressedSize.Record(limitedBody.BytesRead);
            AppDiagnostics.IngestionV3ActiveStreams.Add(-1);
        }

        if (response.Received > 0 && response.Invalid == response.Received)
        {
            return Problem(response, StatusCodes.Status422UnprocessableEntity, "The stream did not contain any valid event records.");
        }

        return Results.Json(response, EventIngestionJsonContext.Default.EventIngestionV3Response);
    }

    private static async Task<EventIngestionV3Response> ProcessBatchAsync(
        EventIngestionV3Processor processor,
        EventIngestionV3ConcurrencyLimiter concurrencyLimiter,
        List<EventIngestionV3BufferedRecord> batch,
        Organization organization,
        Project project,
        CancellationToken cancellationToken)
    {
        try
        {
            using RateLimitLease lease = await concurrencyLimiter.AcquireProcessingAsync(organization.Id, cancellationToken);
            if (!lease.IsAcquired)
            {
                throw new ProcessingConcurrencyRejectedException();
            }

            return await processor.ProcessBufferedAsync(batch, organization, project, cancellationToken);
        }
        finally
        {
            DisposeBatch(batch);
        }
    }

    private static void DisposeBatch(List<EventIngestionV3BufferedRecord> batch)
    {
        foreach (EventIngestionV3BufferedRecord record in batch)
        {
            record.Dispose();
        }

        batch.Clear();
    }

    private static BodyRejection? GetBodyRejection(
        EventPostRequestBodyStream decompressedBody,
        EventIngestionV3RequestBodyState? compressedBodyState)
    {
        if (decompressedBody.RejectedStatusCode is { } decompressedStatusCode)
        {
            return new BodyRejection(decompressedStatusCode, decompressedBody.RejectionReason);
        }

        if (compressedBodyState?.CompressedBody.RejectedStatusCode is { } compressedStatusCode)
        {
            return new BodyRejection(compressedStatusCode, compressedBodyState.CompressedBody.RejectionReason);
        }

        return null;
    }

    private static IResult Problem(EventIngestionV3Response response, int statusCode, string? title, string? detail = null)
    {
        Dictionary<string, object?>? extensions = null;
        if (response.Received > 0)
        {
            extensions = new Dictionary<string, object?>
            {
                ["partial_result"] = response,
                ["retry_guidance"] = "Some earlier events were processed. Retry the complete request; event ids make replay idempotent."
            };
        }

        return Results.Problem(statusCode: statusCode, title: title, detail: detail, extensions: extensions);
    }

    private sealed record BodyRejection(int StatusCode, string? Reason);

    private sealed class ProcessingConcurrencyRejectedException : Exception { }
}

internal sealed class EventIngestionV3EndpointMetadata
{
    public static EventIngestionV3EndpointMetadata Instance { get; } = new();

    private EventIngestionV3EndpointMetadata() { }
}
