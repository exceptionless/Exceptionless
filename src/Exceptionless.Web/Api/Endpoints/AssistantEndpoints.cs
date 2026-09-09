using System.Globalization;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Serialization;
using Exceptionless.Web.Assistant;
using Microsoft.AspNetCore.Mvc;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace Exceptionless.Web.Api.Endpoints;

public static class AssistantEndpoints
{
    internal const string FullLoggingHeaderName = "X-Exie-Full-Logging";
    private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureExceptionlessApiDefaults();

    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("api/v2/assistant/conversation-sharing", GetConversationSharingAsync)
            .WithName("GetAssistantConversationSharing")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .Produces<AssistantConversationSharingSettings>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPut("api/v2/assistant/conversation-sharing", SetConversationSharingAsync)
            .WithName("SetAssistantConversationSharing")
            .WithDescription("Saves the current user's choice. Null follows the admin default; true and false remain explicit choices when the default changes.")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .Produces<AssistantConversationSharingSettings>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet("api/v2/assistant/access", GetAccessAsync)
            .WithName("GetAssistantAccess")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .Produces<AssistantAccessResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status426UpgradeRequired);

        endpoints.MapPost("api/v2/assistant/chat", StreamChatAsync)
            .WithName("StreamAssistantChat")
            .WithDescription("The X-Exie-Full-Logging response header indicates whether the browser should record prompts and responses for this turn. Missing or false disables conversation logging.")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(256 * 1024))
            .Produces(StatusCodes.Status200OK, contentType: "application/x-ndjson")
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status426UpgradeRequired)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<IResult> StreamChatAsync(
        AssistantChatRequest request,
        HttpContext httpContext,
        AssistantAccessService assistantAccessService,
        AssistantUsageService assistantUsageService,
        AssistantService assistantService,
        AssistantConversationSharingService conversationSharingService,
        TimeProvider timeProvider,
        ILogger<AssistantService> logger)
    {
        string? organizationId = request.OrganizationId?.Trim();
        var access = await assistantAccessService.GetAccessAsync(httpContext.Request, organizationId);
        var accessFailure = MapAccessFailure(access);
        if (accessFailure is not null)
            return accessFailure;

        if (request.Messages is null
            || request.Messages.Count == 0
            || request.Messages.Any(message => message is null)
            || request.Messages.All(message => String.IsNullOrWhiteSpace(message?.Content)))
            return HttpResults.ValidationProblem(new Dictionary<string, string[]> { ["messages"] = ["At least one message is required."] });

        string? conversationId = NormalizeConversationId(request.ConversationId);
        if (request.ConversationId is not null && conversationId is null)
            return HttpResults.ValidationProblem(new Dictionary<string, string[]> { ["conversation_id"] = ["The conversation id must be a valid GUID."] });

        string? userId = httpContext.User.GetUserId();
        if (String.IsNullOrWhiteSpace(userId))
            return HttpResults.Unauthorized();

        request = request with
        {
            OrganizationId = organizationId,
            ConversationId = conversationId ?? Guid.NewGuid().ToString("N")
        };
        var planOptions = access.PlanOptions!;
        await using var turnReservation = await assistantUsageService.TryStartTurnAsync(organizationId, planOptions);
        if (!turnReservation.Allowed)
        {
            string? detail = turnReservation.Message;
            if (turnReservation.ResetAtUtc is not null)
            {
                long retryAfterSeconds = Math.Max(1, (long)Math.Ceiling((turnReservation.ResetAtUtc.Value - timeProvider.GetUtcNow()).TotalSeconds));
                httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                detail = $"{detail} It resets at {turnReservation.ResetAtUtc.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}.";
            }

            return HttpResults.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Exie usage limit reached.",
                detail: detail);
        }

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = "application/x-ndjson";
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.Append("X-Accel-Buffering", "no");
        bool fullLoggingEnabled = (await conversationSharingService.GetAsync(userId))?.Enabled == true;

        using var turnCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted);
        turnCancellationSource.CancelAfter(TimeSpan.FromSeconds(AssistantLimits.MaximumTurnDurationSeconds));
        using var diagnostics = new AssistantTurnDiagnostics(logger, timeProvider, organizationId!, request.ConversationId!, httpContext.TraceIdentifier, httpContext.RequestAborted);
        var response = assistantService.StreamAsync(request, userId, planOptions, diagnostics, turnCancellationSource.Token);
        await WriteResponseAsync(httpContext, response, assistantUsageService, organizationId!, diagnostics, turnCancellationSource.Token, fullLoggingEnabled);

        return HttpResults.Empty;
    }

    internal static async Task WriteResponseAsync(
        HttpContext httpContext,
        IAsyncEnumerable<AssistantStreamEvent> response,
        AssistantUsageService assistantUsageService,
        string organizationId,
        AssistantTurnDiagnostics diagnostics,
        CancellationToken cancellationToken,
        bool fullLoggingEnabled = false)
    {
        httpContext.Response.Headers[FullLoggingHeaderName] = fullLoggingEnabled ? "true" : "false";
        bool responseFailed = false;
        try
        {
            await foreach (var item in response.WithCancellation(cancellationToken))
            {
                diagnostics.Observe(item);
                responseFailed |= item.Type == "error";
                string stage = diagnostics.Stage;
                diagnostics.Stage = "response_write";
                await JsonSerializer.SerializeAsync(httpContext.Response.Body, item, s_jsonOptions, cancellationToken);
                await httpContext.Response.WriteAsync("\n", cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);
                diagnostics.Stage = stage;
            }

            diagnostics.Finish(responseFailed ? "failed" : "completed");
            if (responseFailed)
                await assistantUsageService.RecordTurnFailedAsync(organizationId);
            else
                await assistantUsageService.RecordTurnCompletedAsync(organizationId);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            // The browser closing or stopping the stream is expected.
            diagnostics.Finish("cancelled", "client_disconnected");
            await assistantUsageService.RecordTurnCancelledAsync(organizationId);
        }
        catch (OperationCanceledException)
        {
            string failureCode = cancellationToken.IsCancellationRequested ? "turn_timeout"
                : diagnostics.Stage is "provider_request" or "provider_stream" ? "provider_timeout" : "operation_cancelled";
            diagnostics.Finish("failed", failureCode);
            await assistantUsageService.RecordTurnFailedAsync(organizationId);
            var error = AssistantStreamEvent.Error("Exie took too long to complete this response. Try narrowing the question.");
            await JsonSerializer.SerializeAsync(httpContext.Response.Body, error, s_jsonOptions, CancellationToken.None);
            await httpContext.Response.WriteAsync("\n", CancellationToken.None);
        }
        catch (Exception ex)
        {
            string failureCode = ex switch
            {
                AssistantProviderException providerException => providerException.FailureCode,
                _ when diagnostics.Stage == "response_write" => "response_write_error",
                _ when diagnostics.Stage == "tool_execution" => "tool_execution_error",
                HttpRequestException when diagnostics.Stage is "provider_request" or "provider_stream" => "provider_transport_error",
                JsonException when diagnostics.Stage == "provider_stream" => "invalid_provider_response",
                IOException when diagnostics.Stage == "provider_stream" => "provider_stream_error",
                _ => "internal_error"
            };
            diagnostics.Finish("failed", failureCode, ex);
            await assistantUsageService.RecordTurnFailedAsync(organizationId);
            var error = AssistantStreamEvent.Error(ex is AssistantProviderException ? ex.Message : "Exie could not complete this request.");
            await JsonSerializer.SerializeAsync(httpContext.Response.Body, error, s_jsonOptions, CancellationToken.None);
            await httpContext.Response.WriteAsync("\n", CancellationToken.None);
        }
    }

    private static async Task<IResult> GetAccessAsync(
        [FromQuery(Name = "organization_id")] string? organizationId,
        HttpContext httpContext,
        AssistantAccessService assistantAccessService)
    {
        var access = await assistantAccessService.GetAccessAsync(httpContext.Request, organizationId);
        return HttpResults.Ok(access.ToResponse());
    }

    private static async Task<IResult> GetConversationSharingAsync(HttpContext httpContext, AssistantConversationSharingService service)
    {
        string? userId = httpContext.User.GetUserId();
        if (String.IsNullOrWhiteSpace(userId))
            return HttpResults.Unauthorized();

        var settings = await service.GetAsync(userId);
        return settings is null ? HttpResults.NotFound() : HttpResults.Ok(settings);
    }

    private static async Task<IResult> SetConversationSharingAsync(UpdateAssistantConversationSharing request, HttpContext httpContext, AssistantConversationSharingService service)
    {
        string? userId = httpContext.User.GetUserId();
        if (String.IsNullOrWhiteSpace(userId))
            return HttpResults.Unauthorized();

        var settings = await service.SetAsync(userId, request.Enabled);
        return settings is null ? HttpResults.NotFound() : HttpResults.Ok(settings);
    }

    internal static IResult? MapAccessFailure(AssistantAccessDecision access) => access.Reason switch
    {
        AssistantAccessReason.Available => null,
        AssistantAccessReason.Disabled => HttpResults.NotFound(),
        AssistantAccessReason.NotConfigured => HttpResults.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Exie is not configured.",
            detail: "Set EX_Assistant__ApiKey on the web service to configure Exie."),
        AssistantAccessReason.OrganizationRequired => HttpResults.ValidationProblem(
            new Dictionary<string, string[]> { ["organization_id"] = ["Select an organization to use Exie."] }),
        AssistantAccessReason.OrganizationNotAccessible => HttpResults.Forbid(),
        AssistantAccessReason.UpgradeRequired => HttpResults.Problem(
            statusCode: StatusCodes.Status426UpgradeRequired,
            title: access.Message),
        _ => HttpResults.NotFound()
    };

    private static string? NormalizeConversationId(string? conversationId)
    {
        if (String.IsNullOrWhiteSpace(conversationId))
            return null;

        return Guid.TryParse(conversationId, out var parsed) ? parsed.ToString("N") : null;
    }
}
