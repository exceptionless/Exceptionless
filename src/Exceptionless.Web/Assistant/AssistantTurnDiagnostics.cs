using System.Diagnostics;
using System.Text.Json;
using Exceptionless.Web.Mcp;

namespace Exceptionless.Web.Assistant;

internal sealed class AssistantTurnDiagnostics : IDisposable
{
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly long _started;
    private readonly Activity? _activity;
    private readonly IDisposable? _scope;
    private bool _finished;
    private double? _firstTextDuration;
    private string? _failureCode;

    public AssistantTurnDiagnostics(ILogger logger, TimeProvider timeProvider, string organizationId, string conversationId, string requestId)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _started = timeProvider.GetTimestamp();
        _activity = AppDiagnostics.StartActivity("assistant.turn");
        TurnId = Guid.NewGuid().ToString("N");
        OrganizationId = organizationId;
        ConversationId = conversationId;
        RequestId = requestId;
        TraceId = Activity.Current?.TraceId.ToString();
        _activity?.SetTag("assistant.turn.id", TurnId);
        _activity?.SetTag("organization.id", organizationId);
        _activity?.SetTag("assistant.conversation.id", conversationId);
        _scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AssistantTurnId"] = TurnId,
            ["OrganizationId"] = organizationId,
            ["ConversationId"] = conversationId,
            ["RequestId"] = requestId,
            ["TraceId"] = TraceId
        });
    }

    public string TurnId { get; }
    public string OrganizationId { get; }
    public string ConversationId { get; }
    public string RequestId { get; }
    public string? TraceId { get; }
    public string? Model { get; set; }
    public string Stage { get; set; } = "initializing";
    public int ProviderRequests { get; private set; }
    public int ToolCalls { get; private set; }
    public int ToolFailures { get; private set; }
    public int ToolRounds { get; set; }
    public int MalformedResponseRetries { get; set; }
    public string? LastTool { get; private set; }
    public string? LastToolError { get; private set; }
    public AssistantProviderDiagnostics? Provider { get; private set; }

    public AssistantProviderDiagnostics StartProviderRequest(int inputCharacters, bool allowTools, CancellationToken cancellationToken)
    {
        Stage = "provider_request";
        ProviderRequests++;
        Provider = new AssistantProviderDiagnostics(_logger, _timeProvider, this, inputCharacters, allowTools, cancellationToken);
        return Provider;
    }

    public void Observe(AssistantStreamEvent item)
    {
        if (item.Type == "error")
            _failureCode ??= item.FailureCode ?? "response_error";
        if (item.Type == "text_delta" && !String.IsNullOrEmpty(item.Text))
            _firstTextDuration ??= ElapsedMilliseconds;
    }

    public void StartTool(string name)
    {
        Stage = "tool_execution";
        LastTool = GetToolName(name);
        ToolCalls++;
    }

    public void RecordToolResult(string result, double durationMilliseconds)
    {
        using var document = JsonDocument.Parse(result);
        var root = document.RootElement;
        bool failed = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False;
        string? errorCode = null;
        if (failed)
        {
            ToolFailures++;
            errorCode = "tool_error";
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String)
                errorCode = GetToolErrorCode(code.GetString());
            LastToolError = errorCode;
            _logger.LogWarning("Assistant tool {ToolName} failed with {ToolErrorCode} in {DurationMs} ms for turn {AssistantTurnId}",
                LastTool, errorCode, durationMilliseconds, TurnId);
        }

        AppDiagnostics.AssistantToolDuration.Record(durationMilliseconds,
            new("tool", LastTool), new("outcome", failed ? "failed" : "completed"), new("reason", errorCode ?? "none"));
    }

    public void Finish(string outcome, string? failureCode = null, Exception? exception = null)
    {
        if (_finished)
            return;
        _finished = true;
        string reason = failureCode ?? _failureCode ?? "none";
        _activity?.SetTag("assistant.outcome", outcome);
        _activity?.SetTag("assistant.failure.reason", reason);
        _activity?.SetTag("assistant.stage", Stage);
        _activity?.SetTag("assistant.model", Model);
        _activity?.SetTag("assistant.provider.requests", ProviderRequests);
        _activity?.SetTag("assistant.tool.calls", ToolCalls);
        if (outcome == "failed")
            _activity?.SetStatus(ActivityStatusCode.Error, reason);

        AppDiagnostics.AssistantTurnDuration.Record(ElapsedMilliseconds,
            new("outcome", outcome), new("reason", reason), new("stage", Stage));

        // Include correlation in the message as well as the scope: the production console
        // formatter does not render scope properties, and streaming errors retain HTTP 200.
        var level = outcome == "failed" ? exception is null ? LogLevel.Warning : LogLevel.Error : LogLevel.Information;
        _logger.Log(level,
            "Assistant turn {AssistantTurnId} {Outcome}: reason={FailureReason} stage={Stage} duration={DurationMs} ms first_text={FirstTextDurationMs} ms organization={OrganizationId} conversation={ConversationId} request={RequestId} trace={TraceId} model={Model} provider_requests={ProviderRequests} tool_rounds={ToolRounds} tool_calls={ToolCalls} tool_failures={ToolFailures} last_tool={LastTool} last_tool_error={LastToolError} malformed_retries={MalformedResponseRetries} generation={ProviderGenerationId} provider_model={ProviderModel} provider={ProviderName} status={ProviderStatusCode} finish={ProviderFinishReason} usage_received={ProviderUsageReceived} reasoning_tokens={ReasoningTokens} exception_type={ExceptionType} exception_stack={ExceptionStackTrace}",
            TurnId, outcome, reason, Stage, ElapsedMilliseconds, _firstTextDuration, OrganizationId, ConversationId, RequestId, TraceId,
            Model, ProviderRequests, ToolRounds, ToolCalls, ToolFailures, LastTool, LastToolError, MalformedResponseRetries,
            Provider?.GenerationId, Provider?.Model, Provider?.ProviderName, Provider?.StatusCode, Provider?.FinishReason,
            Provider?.UsageReceived, Provider?.ReasoningTokens, exception?.GetType().FullName, exception?.StackTrace);
    }

    private double ElapsedMilliseconds => _timeProvider.GetElapsedTime(_started).TotalMilliseconds;

    public void Dispose()
    {
        _scope?.Dispose();
        _activity?.Dispose();
    }

    private static string GetToolName(string name) => name switch
    {
        "get_event" or "get_stack" or "get_project_setup" or "get_stack_events" or "list_projects" or "search_stacks"
            or "update_stack_status" or "snooze_stack" or "set_stack_critical" or "add_stack_reference_link" or "remove_stack_reference_link" => name,
        _ => "unknown"
    };

    private static string GetToolErrorCode(string? code) => code switch
    {
        McpErrorCodes.ContextMismatch or McpErrorCodes.ContextRequired or McpErrorCodes.Forbidden or McpErrorCodes.InvalidClientPlatform
            or McpErrorCodes.InvalidCursor or McpErrorCodes.InvalidDetailSize or McpErrorCodes.InvalidFilter or McpErrorCodes.InvalidGroupBy
            or McpErrorCodes.InvalidId or McpErrorCodes.InvalidInterval or McpErrorCodes.InvalidLimit or McpErrorCodes.InvalidReferenceUrl
            or McpErrorCodes.InvalidSnooze or McpErrorCodes.InvalidSort or McpErrorCodes.InvalidStatus or McpErrorCodes.InvalidTimeRange
            or McpErrorCodes.InvalidVersion or McpErrorCodes.NotAccessible or McpErrorCodes.NotFound or McpErrorCodes.QueryFailed
            or McpErrorCodes.UnknownFilterField or "tool_call_limit_reached" or "project_search_limit_reached" => code,
        _ => "tool_error"
    };
}
