using System.Text.Json;
using System.Threading.RateLimiting;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Utility;
using Exceptionless.Web.Utility.Handlers;
using Foundatio.Repositories;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Endpoints;

public static class EventIngestionV3Endpoints
{
    internal const string BusyRetryAfterSeconds = "5";
    internal const string UnavailableRetryAfterSeconds = "30";

    private const string Description = """
        Submits one or more events. The request body can be a single JSON event object, a JSON array of
        event objects, or event objects separated by whitespace or newlines (NDJSON). Events are read and
        processed as they arrive, so a request can stream a large number of events.

        Every event property is optional. A request is acknowledged after every event reaches an outcome:
        stored, discarded, duplicate, blocked by the plan limit, or invalid. Invalid events are reported
        with their position in the request and do not prevent the remaining events from being processed.
        Include an `id` on each event to make resending a request safe.
        """;

    private static readonly HashSet<string> _supportedMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json",
        "application/x-ndjson",
        "application/ndjson",
        "application/jsonl",
        "application/x-jsonl",
        "application/jsonlines",
        "application/x-jsonlines"
    };

    public static IEndpointRouteBuilder MapEventIngestionV3(this IEndpointRouteBuilder endpoints, EventIngestionV3Options options)
    {
        var group = endpoints.MapGroup("/api/v3")
            .RequireAuthorization(AuthorizationRoles.ClientPolicy)
            .WithTags("Event Ingestion V3");

        Map(group.MapPost("/events", HandleDefaultProjectAsync), options)
            .WithName("PostEventsV3")
            .WithSummary("Submit events to the project of the API key.");
        Map(group.MapPost("/projects/{projectId:objectid}/events", HandleProjectAsync), options)
            .WithName("PostEventsByProjectV3")
            .WithSummary("Submit events to a project.");

        return endpoints;
    }

    private static RouteHandlerBuilder Map(RouteHandlerBuilder builder, EventIngestionV3Options options)
    {
        builder
            .WithDescription(Description)
            .WithMetadata(EventIngestionV3EndpointMetadata.Instance)
            .Accepts<EventIngestionV3Event>("application/json", "application/x-ndjson")
            .Produces<EventIngestionV3Response>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status413RequestEntityTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithRequestTimeout(new RequestTimeoutPolicy
            {
                Timeout = options.RequestTimeout,
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

    private static Task<IResult> HandleDefaultProjectAsync(HttpContext httpContext, [AsParameters] EventIngestionV3Services services, CancellationToken cancellationToken) =>
        HandleAsync(httpContext, null, services, cancellationToken);

    private static Task<IResult> HandleProjectAsync(HttpContext httpContext, string projectId, [AsParameters] EventIngestionV3Services services, CancellationToken cancellationToken) =>
        HandleAsync(httpContext, projectId, services, cancellationToken);

    private static async Task<IResult> HandleAsync(HttpContext httpContext, string? projectId, EventIngestionV3Services services, CancellationToken cancellationToken)
    {
        var request = httpContext.Request;
        var options = services.Options.EventIngestionV3;
        if (!options.Enabled)
        {
            return Results.NotFound();
        }

        if (!await services.SystemSettingsService.IsEventSubmissionEnabledAsync())
        {
            return Unavailable(httpContext, "Event submission is temporarily disabled.");
        }

        if (!TryGetMediaType(request, out string? mediaType, out string? charSet))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Content-Type must be application/json or application/x-ndjson with UTF-8 encoding.");
        }

        if (!TryGetContentEncoding(request, out string? contentEncoding))
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

        var project = await services.ProjectRepository.GetByIdAsync(projectId, o => o.Cache());
        if (project is null || !request.CanAccessOrganization(project.OrganizationId))
        {
            return Results.NotFound();
        }

        if ((options.AllowedProjectIds.Count > 0 && !options.AllowedProjectIds.Contains(project.Id))
            || (options.AllowedOrganizationIds.Count > 0 && !options.AllowedOrganizationIds.Contains(project.OrganizationId)))
        {
            return Results.NotFound();
        }

        var organization = await services.OrganizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());
        if (organization is null)
        {
            return Results.NotFound();
        }

        if (organization.IsSuspended)
        {
            return Results.Problem(statusCode: StatusCodes.Status402PaymentRequired, title: "The organization is suspended and cannot accept events.");
        }

        if (await services.UsageService.GetEventsLeftAsync(organization.Id) <= 0)
        {
            await services.UsageService.IncrementBlockedAsync(organization.Id, project.Id);
            return Results.Problem(statusCode: StatusCodes.Status402PaymentRequired, title: "The organization has reached its event limit.");
        }

        using RateLimitLease organizationStreamLease = await services.ConcurrencyLimiter.AcquireOrganizationActiveStreamAsync(organization.Id, cancellationToken);
        if (!organizationStreamLease.IsAcquired)
        {
            return Busy(httpContext, null, "Too many event streams are open for this organization.");
        }

        request.SetProject(project);
        var limitedBody = new EventPostRequestBodyStream(
            request.Body,
            options.MaximumDecompressedBodySize,
            "The decompressed request body is too large.",
            StatusCodes.Status400BadRequest,
            "The compressed request body is invalid.");
        request.Body = limitedBody;

        var eventPostInfo = new EventPostInfo
        {
            ApiVersion = 3,
            CharSet = charSet,
            ContentEncoding = contentEncoding,
            IpAddress = request.GetClientIpAddress(),
            MediaType = mediaType,
            OrganizationId = organization.Id,
            ProjectId = project.Id,
            ClientKeyHash = request.GetClientKeyHash(),
            UserAgent = request.GetClientUserAgent()
        };

        var compressedBodyState = httpContext.Features.Get<EventIngestionV3RequestBodyState>();
        var reader = new EventIngestionV3StreamReader(request.BodyReader, options.MaximumEventSize, services.Processor.SerializerOptions);
        var response = new EventIngestionV3Response();
        var batch = new List<EventIngestionV3Record>(options.MicroBatchSize);
        long batchBytes = 0;

        AppDiagnostics.IngestionV3ActiveStreams.Add(1);
        try
        {
            while (await reader.ReadAsync(cancellationToken) is { } record)
            {
                if (record.Event is null)
                {
                    response.Received++;
                    response.Invalid++;
                    response.AddError(record.Index, null, record.ErrorCode!, record.ErrorMessage!);
                    AppDiagnostics.IngestionV3Received.Add(1);
                    AppDiagnostics.IngestionV3Invalid.Add(1);
                    continue;
                }

                if (batch.Count > 0 && batchBytes + record.Size > options.MaximumMicroBatchBytes)
                {
                    await ProcessBatchAsync();
                }

                batch.Add(new EventIngestionV3Record(record.Index, record.Event));
                batchBytes += record.Size;
                if (batch.Count >= options.MicroBatchSize)
                {
                    await ProcessBatchAsync();
                }
            }

            if (GetBodyRejection(limitedBody, compressedBodyState) is { } rejection)
            {
                return Problem(response, rejection.StatusCode, rejection.Reason);
            }

            if (batch.Count > 0)
            {
                await ProcessBatchAsync();
            }
        }
        catch (Exception ex) when ((ex is JsonException or InvalidDataException) && GetBodyRejection(limitedBody, compressedBodyState) is not null)
        {
            var rejection = GetBodyRejection(limitedBody, compressedBodyState)!;
            return Problem(response, rejection.StatusCode, rejection.Reason);
        }
        catch (JsonException ex)
        {
            return Problem(response, StatusCodes.Status400BadRequest, "The request body is not a valid JSON array of events.", ex.Message);
        }
        catch (InvalidDataException ex)
        {
            return Problem(response, StatusCodes.Status400BadRequest, "The compressed request body is invalid.", ex.Message);
        }
        catch (ProcessingCapacityUnavailableException)
        {
            return Busy(httpContext, response, "Event processing capacity is busy.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppDiagnostics.IngestionV3RequestFailures.Add(1);
            services.LoggerFactory.CreateLogger(typeof(EventIngestionV3Endpoints)).LogError(ex, "Error processing V3 events for project {ProjectId}: {Message}", project.Id, ex.Message);
            return Unavailable(httpContext, "Events could not be processed.", response);
        }
        finally
        {
            AppDiagnostics.IngestionV3DecompressedSize.Record(limitedBody.BytesRead);
            AppDiagnostics.IngestionV3ActiveStreams.Add(-1);
        }

        if (response.Failed > 0)
        {
            AppDiagnostics.IngestionV3RequestFailures.Add(1);
            return Unavailable(httpContext, "Some events could not be processed.", response);
        }

        if (response.Received > 0 && response.Invalid == response.Received)
        {
            return Problem(response, StatusCodes.Status422UnprocessableEntity, "The request did not contain any valid events.");
        }

        return Results.Ok(response);

        async Task ProcessBatchAsync()
        {
            using (RateLimitLease lease = await services.ConcurrencyLimiter.AcquireProcessingAsync(organization.Id, cancellationToken))
            {
                if (!lease.IsAcquired)
                {
                    throw new ProcessingCapacityUnavailableException();
                }

                AppDiagnostics.IngestionV3Received.Add(batch.Count);
                response.Add(await services.Processor.ProcessAsync(batch, organization, project, eventPostInfo, cancellationToken));
            }

            batch.Clear();
            batchBytes = 0;
        }
    }

    private static bool TryGetMediaType(HttpRequest request, out string? mediaType, out string? charSet)
    {
        mediaType = null;
        charSet = null;
        if (String.IsNullOrEmpty(request.ContentType))
        {
            return true;
        }

        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out MediaTypeHeaderValue? contentType))
        {
            return false;
        }

        mediaType = contentType.MediaType.Value;
        charSet = contentType.Charset.Value;
        bool isJson = mediaType is not null
            && (_supportedMediaTypes.Contains(mediaType)
                || (mediaType.StartsWith("application/", StringComparison.OrdinalIgnoreCase) && mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)));
        bool isUtf8 = String.IsNullOrEmpty(charSet)
            || String.Equals(charSet, "utf-8", StringComparison.OrdinalIgnoreCase)
            || String.Equals(charSet, "utf8", StringComparison.OrdinalIgnoreCase);
        return isJson && isUtf8;
    }

    private static bool TryGetContentEncoding(HttpRequest request, out string? contentEncoding)
    {
        string[] encodings = request.Headers.ContentEncoding
            .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
            .ToArray();
        contentEncoding = encodings.FirstOrDefault();
        if (encodings.Length > 1)
        {
            return false;
        }

        return String.IsNullOrEmpty(contentEncoding)
            || String.Equals(contentEncoding, "identity", StringComparison.OrdinalIgnoreCase)
            || String.Equals(contentEncoding, "gzip", StringComparison.OrdinalIgnoreCase)
            || String.Equals(contentEncoding, "br", StringComparison.OrdinalIgnoreCase);
    }

    private static BodyRejection? GetBodyRejection(EventPostRequestBodyStream decompressedBody, EventIngestionV3RequestBodyState? compressedBodyState)
    {
        if (compressedBodyState?.CompressedBody.RejectedStatusCode is { } compressedStatusCode)
        {
            return new BodyRejection(compressedStatusCode, compressedBodyState.CompressedBody.RejectionReason);
        }

        if (decompressedBody.RejectedStatusCode is { } decompressedStatusCode)
        {
            return new BodyRejection(decompressedStatusCode, decompressedBody.RejectionReason);
        }

        return null;
    }

    private static IResult Busy(HttpContext httpContext, EventIngestionV3Response? response, string title)
    {
        httpContext.Response.Headers.RetryAfter = BusyRetryAfterSeconds;
        return Problem(response, StatusCodes.Status429TooManyRequests, title);
    }

    private static IResult Unavailable(HttpContext httpContext, string title, EventIngestionV3Response? response = null)
    {
        httpContext.Response.Headers.RetryAfter = UnavailableRetryAfterSeconds;
        return Problem(response, StatusCodes.Status503ServiceUnavailable, title);
    }

    private static IResult Problem(EventIngestionV3Response? response, int statusCode, string? title, string? detail = null)
    {
        Dictionary<string, object?>? extensions = null;
        if (response is { Received: > 0 })
        {
            bool isRetryable = statusCode is StatusCodes.Status429TooManyRequests or StatusCodes.Status503ServiceUnavailable;
            extensions = new Dictionary<string, object?>
            {
                ["partial_result"] = response,
                ["retry_guidance"] = isRetryable
                    ? "Resend the request after the Retry-After delay. Events with an id that were already stored are acknowledged as duplicates."
                    : "The events in partial_result were processed. Resending this request unchanged fails the same way; correct it or send the remaining events separately. Events with an id that were already stored are acknowledged as duplicates."
            };
        }

        return Results.Problem(statusCode: statusCode, title: title, detail: detail, extensions: extensions);
    }

    private sealed record BodyRejection(int StatusCode, string? Reason);

    private sealed class ProcessingCapacityUnavailableException : Exception;
}

internal sealed record EventIngestionV3Services(
    EventIngestionV3Processor Processor,
    EventIngestionV3ConcurrencyLimiter ConcurrencyLimiter,
    IProjectRepository ProjectRepository,
    IOrganizationRepository OrganizationRepository,
    UsageService UsageService,
    SystemSettingsService SystemSettingsService,
    AppOptions Options,
    ILoggerFactory LoggerFactory);

internal sealed class EventIngestionV3EndpointMetadata
{
    public static EventIngestionV3EndpointMetadata Instance { get; } = new();

    private EventIngestionV3EndpointMetadata() { }
}
