using System.Diagnostics;
using System.Text.Json;

namespace Exceptionless.Web.Assistant;

internal sealed class AssistantProviderDiagnostics(
    ILogger logger,
    TimeProvider timeProvider,
    AssistantTurnDiagnostics turn,
    int inputCharacters,
    bool allowTools,
    CancellationToken cancellationToken) : IDisposable
{
    private readonly long _started = timeProvider.GetTimestamp();
    private readonly Activity? _activity = AppDiagnostics.AssistantActivitySource.StartActivity("assistant.provider");
    private bool _finished;
    private bool _receivedError;

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
        if (response.Headers.TryGetValues("X-Generation-Id", out var values))
            GenerationId = SafeMetadata(values.FirstOrDefault());
        turn.Stage = "provider_stream";
    }

    public void ObserveChunk(JsonElement chunk)
    {
        if (chunk.ValueKind != JsonValueKind.Object)
            return;
        GenerationId = GetMetadata(chunk, "id") ?? GenerationId;
        Model = GetMetadata(chunk, "model") ?? Model;
        ProviderName = GetMetadata(chunk, "provider") ?? ProviderName;
        _receivedError |= chunk.TryGetProperty("error", out _);
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
        Finish(outcome, outputCharacters, toolCalls, receivedDone);
    }

    public void RecordException(Exception exception)
    {
        string outcome = exception switch
        {
            AssistantProviderException when StatusCode is >= 400 => "http_error",
            AssistantProviderException => "provider_error",
            OperationCanceledException => GetCancellationOutcome(),
            HttpRequestException => "provider_transport_error",
            JsonException => "invalid_provider_response",
            IOException => "provider_stream_error",
            _ => "internal_error"
        };
        Finish(outcome);
    }

    private void Finish(string outcome, int? outputCharacters = null, int? toolCalls = null, bool receivedDone = false)
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
        AppDiagnostics.AssistantProviderDuration.Record(duration, new KeyValuePair<string, object?>("outcome", outcome));
        logger.Log(outcome is "completed" or "cancelled" ? LogLevel.Information : LogLevel.Warning,
            "Assistant provider request {ProviderRequestNumber} {ProviderOutcome} for turn {AssistantTurnId}: duration={DurationMs} ms generation={ProviderGenerationId} model={ProviderModel} provider={ProviderName} status={ProviderStatusCode} finish={ProviderFinishReason} input_characters={InputCharacters} output_characters={OutputCharacters} tools_allowed={ToolsAllowed} tool_calls={ToolCalls} usage_received={UsageReceived} prompt_tokens={PromptTokens} completion_tokens={CompletionTokens} reasoning_tokens={ReasoningTokens} received_done={ReceivedDone}",
            turn.ProviderRequests, outcome, turn.TurnId, duration, GenerationId, Model ?? turn.Model, ProviderName, StatusCode,
            FinishReason, inputCharacters, outputCharacters, allowTools, toolCalls, UsageReceived, PromptTokens, CompletionTokens, ReasoningTokens, receivedDone);
        _activity?.Dispose();
    }

    public void Dispose() => Finish(StatusCode is >= 400 ? "http_error"
        : _receivedError || FinishReason == "error" ? "provider_error"
        : cancellationToken.IsCancellationRequested ? GetCancellationOutcome() : "interrupted");

    private string GetCancellationOutcome() => turn.IsClientDisconnected ? "cancelled"
        : cancellationToken.IsCancellationRequested ? "turn_timeout" : "provider_timeout";

    private static string? GetMetadata(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? SafeMetadata(property.GetString()) : null;

    private static long? GetTokenCount(JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out long value)
            ? Math.Max(0, value) : null;

    private static string? SafeMetadata(string? value)
        => value is { Length: > 0 and <= 128 } && value.All(character => Char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '/' or '.' or ':' or '~' or ' ')
            ? value : null;
}
