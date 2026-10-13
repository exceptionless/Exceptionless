using System.Diagnostics;
using System.Text.Json;

namespace Exceptionless.Web.Assistant;

internal sealed class AssistantProviderDiagnostics(
    ILogger logger,
    TimeProvider timeProvider,
    AssistantTurnDiagnostics turn,
    CancellationToken cancellationToken) : IDisposable
{
    private readonly long _started = timeProvider.GetTimestamp();
    private readonly Activity? _activity = AppDiagnostics.AssistantActivitySource.StartActivity("assistant.provider");
    private bool _finished;
    private bool _receivedError;
    private double? _headersDuration;
    private double? _firstChunkDuration;
    private string? _errorCode;
    private string? _errorType;
    private string? _upstreamErrorCode;
    private string? _errorMessage;

    public string? GenerationId { get; private set; }
    public string? Model { get; private set; }
    public string? ProviderName { get; private set; }
    public int? StatusCode { get; private set; }
    public string? FinishReason { get; private set; }
    public bool UsageReceived { get; private set; }
    public long? PromptTokens { get; private set; }
    public long? CompletionTokens { get; private set; }
    public long? ReasoningTokens { get; private set; }

    public void ObserveResponse(HttpResponseMessage response)
    {
        StatusCode = (int)response.StatusCode;
        _headersDuration = timeProvider.GetElapsedTime(_started).TotalMilliseconds;
        if (response.Headers.TryGetValues("X-Generation-Id", out var values))
            GenerationId = SafeMetadata(values.FirstOrDefault());
        turn.Stage = "provider_stream";
    }

    public void ObserveChunk(JsonElement chunk)
    {
        _firstChunkDuration ??= timeProvider.GetElapsedTime(_started).TotalMilliseconds;
        if (chunk.ValueKind != JsonValueKind.Object)
            return;
        GenerationId = GetMetadata(chunk, "id") ?? GenerationId;
        Model = GetMetadata(chunk, "model") ?? Model;
        ProviderName = GetMetadata(chunk, "provider") ?? ProviderName;
        ObserveError(chunk);
        if (chunk.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            UsageReceived = true;
            PromptTokens = GetTokenCount(usage, "prompt_tokens") ?? PromptTokens;
            CompletionTokens = GetTokenCount(usage, "completion_tokens") ?? CompletionTokens;
            if (usage.TryGetProperty("completion_tokens_details", out var details) && details.ValueKind == JsonValueKind.Object
                && GetTokenCount(details, "reasoning_tokens") is { } value)
                ReasoningTokens = value;
        }
        if (chunk.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            string? reason = GetMetadata(choices[0], "finish_reason");
            if (reason is not null)
                FinishReason = reason is "stop" or "length" or "tool_calls" or "content_filter" or "error" ? reason : "unknown";
        }
    }

    public void ObserveError(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("error", out var error))
            return;

        _receivedError = true;
        GenerationId = GetMetadata(body, "id") ?? GenerationId;
        var metadata = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("metadata", out var value) ? value : default;
        // Read known error fields; arbitrary metadata.raw and flagged_input can contain request data.
        _errorCode = GetMetadata(error, "code");
        _errorType = GetMetadata(metadata, "error_type") ?? GetMetadata(error, "type");
        _upstreamErrorCode = GetMetadata(metadata, "provider_code") ?? GetMetadata(metadata, "provider_error_code");
        ProviderName = GetMetadata(metadata, "provider_name") ?? GetMetadata(body, "provider") ?? ProviderName;
        if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            _errorMessage = message.GetString();
    }

    public void Complete(int outputCharacters, int toolCalls, bool receivedDone)
    {
        string outcome = _receivedError ? "provider_error" : FinishReason switch
        {
            "length" => "output_limit",
            "content_filter" => "content_filter",
            "error" => "provider_error",
            _ when outputCharacters == 0 && toolCalls == 0 => "empty_response",
            _ when !receivedDone && FinishReason is null => "incomplete_stream",
            _ => "completed"
        };
        Finish(outcome);
    }

    public void RecordException(Exception exception)
    {
        string outcome = exception switch
        {
            AssistantProviderException providerException => providerException.FailureCode,
            OperationCanceledException => GetCancellationOutcome(),
            HttpRequestException => "provider_transport_error",
            JsonException => "invalid_provider_response",
            IOException => "provider_stream_error",
            _ => "internal_error"
        };
        Finish(outcome);
    }

    public void Reject(string reason) => Finish(reason);

    private void Finish(string outcome)
    {
        if (_finished)
            return;
        _finished = true;
        double duration = timeProvider.GetElapsedTime(_started).TotalMilliseconds;
        _activity?.SetTag("assistant.provider.outcome", outcome);
        _activity?.SetTag("assistant.provider.generation_id", GenerationId);
        _activity?.SetTag("assistant.provider.model", Model ?? turn.Model);
        _activity?.SetTag("assistant.provider.name", ProviderName);
        _activity?.SetTag("assistant.provider.finish_reason", FinishReason);
        _activity?.SetTag("http.response.status_code", StatusCode);
        if (outcome is not ("completed" or "cancelled"))
            _activity?.SetStatus(ActivityStatusCode.Error, outcome);
        AppDiagnostics.AssistantProviderDuration.Record(duration, new("model", turn.Model), new("outcome", outcome));
        logger.Log(outcome is "completed" or "cancelled" ? LogLevel.Information : LogLevel.Warning,
            "Assistant provider request {ProviderRequestNumber} {ProviderOutcome} for turn {AssistantTurnId}: duration={DurationMs} ms headers={HeadersDurationMs} ms first_chunk={FirstChunkDurationMs} ms model={ProviderModel} provider={ProviderName} generation={ProviderGenerationId} status={ProviderStatusCode} finish={ProviderFinishReason} prompt_tokens={PromptTokens} completion_tokens={CompletionTokens} reasoning_tokens={ReasoningTokens} error_code={ProviderErrorCode} error_type={ProviderErrorType} upstream_code={UpstreamErrorCode} message={ProviderMessage} organization={OrganizationId} conversation={ConversationId} trace={TraceId}",
            turn.ProviderRequests, outcome, turn.TurnId, duration, _headersDuration, _firstChunkDuration,
            Model ?? turn.Model, ProviderName, GenerationId, StatusCode, FinishReason, PromptTokens, CompletionTokens, ReasoningTokens, _errorCode, _errorType, _upstreamErrorCode, _errorMessage,
            turn.OrganizationId, turn.ConversationId, turn.TraceId);
        _activity?.Dispose();
    }

    public void Dispose() => Finish(StatusCode is < 200 or >= 300 ? "provider_http_error"
        : _receivedError || FinishReason == "error" ? "provider_error"
        : cancellationToken.IsCancellationRequested ? GetCancellationOutcome() : "interrupted");

    private string GetCancellationOutcome()
    {
        string reason = turn.GetCancellationReason(cancellationToken, "provider_timeout");
        return reason == "client_disconnected" ? "cancelled" : reason;
    }

    private static string? GetMetadata(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var property))
            return null;

        return SafeMetadata(property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        });
    }

    private static long? GetTokenCount(JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out long value)
            ? Math.Max(0, value) : null;

    private static string? SafeMetadata(string? value)
        => value is { Length: > 0 and <= 128 } && value.All(character => Char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '/' or '.' or ':' or '~' or ' ')
            ? value : null;
}
