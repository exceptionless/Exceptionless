using System.Diagnostics;
using System.Net.Sockets;
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
    private Dictionary<string, object?>? _request;
    private string? _apiKey;
    private string? _requestSettings;
    private string? _errorDetails;
    private string? _routingDetails;
    private string? _responseBodyState;
    private bool _detailsTruncated;
    private bool _detailsRedacted;
    private int _chunks;
    private double? _headersDuration;
    private double? _firstChunkDuration;
    private double? _lastChunkDuration;
    private string? _exceptionType;
    private string? _transportError;
    private string? _socketError;

    public string? GenerationId { get; private set; }
    public string? Model { get; private set; }
    public string? ProviderName { get; private set; }
    public int? StatusCode { get; private set; }
    public string? FinishReason { get; private set; }
    public bool UsageReceived { get; private set; }
    public long? PromptTokens { get; private set; }
    public long? CompletionTokens { get; private set; }
    public long? ReasoningTokens { get; private set; }
    public string? RequestId { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorType { get; private set; }
    public string? ProviderErrorCode { get; private set; }
    public string? NativeFinishReason { get; private set; }
    public string? ContentType { get; private set; }
    public double? RetryAfterSeconds { get; private set; }
    public string? Outcome { get; private set; }

    public void ObserveRequest(Dictionary<string, object?> request, string? apiKey)
    {
        _request = request;
        _apiKey = apiKey;
        _requestSettings = JsonSerializer.Serialize(new
        {
            max_tokens = request.GetValueOrDefault("max_tokens") as int?,
            temperature = request.GetValueOrDefault("temperature") as double?,
            tool_choice = request.GetValueOrDefault("tool_choice") is "none" ? "none" : "auto",
            tool_count = (request.GetValueOrDefault("tools") as object[])?.Length,
            message_count = (request.GetValueOrDefault("messages") as ICollection<object>)?.Count,
            maximum_prompt_price = AssistantLimits.MaximumProviderPromptPricePerMillionTokens,
            maximum_completion_price = AssistantLimits.MaximumProviderCompletionPricePerMillionTokens
        });
    }

    public void ObserveResponse(HttpResponseMessage response)
    {
        StatusCode = (int)response.StatusCode;
        _headersDuration = timeProvider.GetElapsedTime(_started).TotalMilliseconds;
        ContentType = SafeMetadata(response.Content.Headers.ContentType?.MediaType);
        RequestId = GetHeader(response, "X-Request-Id") ?? GetHeader(response, "Request-Id") ?? GetHeader(response, "X-OpenRouter-Request-Id");
        RetryAfterSeconds = response.Headers.RetryAfter?.Delta?.TotalSeconds
            ?? (response.Headers.RetryAfter?.Date - timeProvider.GetUtcNow())?.TotalSeconds;
        if (response.Headers.TryGetValues("X-Generation-Id", out var values))
        {
            GenerationId = SafeMetadata(values.FirstOrDefault());
        }
        turn.Stage = "provider_stream";
    }

    public void ObserveChunk(JsonElement chunk)
    {
        if (chunk.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        _chunks++;
        _lastChunkDuration = timeProvider.GetElapsedTime(_started).TotalMilliseconds;
        _firstChunkDuration ??= _lastChunkDuration;
        GenerationId = GetMetadata(chunk, "id") ?? GenerationId;
        Model = GetMetadata(chunk, "model") ?? Model;
        ProviderName = GetMetadata(chunk, "provider") ?? ProviderName;
        _receivedError |= chunk.TryGetProperty("error", out _);
        if (chunk.TryGetProperty("error", out _) || chunk.TryGetProperty("openrouter_metadata", out _))
        {
            ObserveErrorDetails(chunk);
        }
        if (chunk.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            UsageReceived = true;
            PromptTokens = GetTokenCount(usage, "prompt_tokens") ?? PromptTokens;
            CompletionTokens = GetTokenCount(usage, "completion_tokens") ?? CompletionTokens;
            if (usage.TryGetProperty("completion_tokens_details", out var details) && details.ValueKind == JsonValueKind.Object
                && GetTokenCount(details, "reasoning_tokens") is { } value)
            {
                ReasoningTokens = value;
            }
        }
        if (chunk.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            string? reason = GetMetadata(choices[0], "finish_reason");
            if (reason is not null)
            {
                FinishReason = reason is "stop" or "length" or "tool_calls" or "content_filter" or "error" ? reason : "unknown";
            }
            NativeFinishReason = GetMetadata(choices[0], "native_finish_reason") ?? NativeFinishReason;
        }
    }

    public async Task ObserveErrorResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Limit reads before parsing, including HTML/proxy errors and responses with no length.
        const int maximumCharacters = 32_768;
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancellationToken));
        var buffer = new char[maximumCharacters + 1];
        int length = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
        _detailsTruncated = length > maximumCharacters;
        _responseBodyState = _detailsTruncated ? "truncated" : length == 0 ? "empty" : "json";
        if (_detailsTruncated || length == 0)
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(new string(buffer, 0, length), new JsonDocumentOptions { MaxDepth = 32 });
            ObserveErrorDetails(document.RootElement);
        }
        catch (JsonException)
        {
            // HTML and malformed JSON can echo arbitrary request data. Keep the response
            // classification and headers, not an unfilterable body.
            _responseBodyState = "invalid_json";
        }
    }

    private void ObserveErrorDetails(JsonElement envelope)
    {
        if (envelope.ValueKind != JsonValueKind.Object)
        {
            _responseBodyState = "invalid_shape";
            return;
        }

        GenerationId = GetMetadata(envelope, "id") ?? GenerationId;
        Model = GetMetadata(envelope, "model") ?? Model;
        ProviderName = GetMetadata(envelope, "provider") ?? ProviderName;
        var messages = _request is not null && _request.TryGetValue("messages", out var value)
            ? JsonSerializer.SerializeToElement(value) : default;
        var details = new AssistantProviderErrorDetails(messages, _apiKey);
        if (envelope.TryGetProperty("error", out var error))
        {
            _receivedError = true;
            _responseBodyState ??= "json";
            ErrorCode = GetCode(error, "code");
            ErrorType = GetMetadata(error, "type");
            if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("metadata", out var metadata))
            {
                ErrorType = GetMetadata(metadata, "error_type") ?? ErrorType;
                ProviderErrorCode = GetCode(metadata, "provider_code") ?? GetCode(metadata, "provider_error_code");
                ProviderName = GetMetadata(metadata, "provider_name") ?? ProviderName;
            }

            _errorDetails = JsonSerializer.Serialize(details.Capture(error));
        }

        if (envelope.TryGetProperty("openrouter_metadata", out var routing))
        {
            _routingDetails = JsonSerializer.Serialize(details.Capture(routing));
        }

        _detailsTruncated |= details.Truncated;
        _detailsRedacted |= details.Redacted;
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

    public void RecordException(Exception exception, int? outputCharacters = null, int? toolCalls = null, bool receivedDone = false)
    {
        _exceptionType = exception.GetType().FullName;
        _transportError = (exception as HttpRequestException)?.HttpRequestError.ToString();
        _socketError = (exception.InnerException as SocketException)?.SocketErrorCode.ToString();
        string outcome = exception switch
        {
            AssistantProviderException providerException => providerException.FailureCode,
            OperationCanceledException => GetCancellationOutcome(),
            HttpRequestException => "provider_transport_error",
            JsonException => "invalid_provider_response",
            IOException => "provider_stream_error",
            _ => "internal_error"
        };
        Finish(outcome, outputCharacters, toolCalls, receivedDone);
    }

    public void Reject(string reason, int outputCharacters, int toolCalls, bool receivedDone) => Finish(reason, outputCharacters, toolCalls, receivedDone);

    private void Finish(string outcome, int? outputCharacters = null, int? toolCalls = null, bool receivedDone = false)
    {
        if (_finished)
        {
            return;
        }
        _finished = true;
        Outcome = outcome;
        double duration = timeProvider.GetElapsedTime(_started).TotalMilliseconds;
        _activity?.SetTag("assistant.provider.outcome", outcome);
        _activity?.SetTag("assistant.provider.generation_id", GenerationId);
        _activity?.SetTag("assistant.provider.model", Model ?? turn.Model);
        _activity?.SetTag("assistant.provider.name", ProviderName);
        _activity?.SetTag("assistant.provider.finish_reason", FinishReason);
        _activity?.SetTag("http.response.status_code", StatusCode);
        _activity?.SetTag("assistant.provider.error.code", ErrorCode);
        _activity?.SetTag("assistant.provider.error.type", ErrorType);
        _activity?.SetTag("assistant.provider.error.provider_code", ProviderErrorCode);
        _activity?.SetTag("assistant.provider.request_id", RequestId);
        if (outcome is not ("completed" or "cancelled"))
        {
            _activity?.SetStatus(ActivityStatusCode.Error, outcome);
        }
        AppDiagnostics.AssistantProviderDuration.Record(duration, new KeyValuePair<string, object?>("outcome", outcome));
        logger.Log(outcome is "completed" or "cancelled" ? LogLevel.Information : LogLevel.Warning,
            "Assistant provider request {ProviderRequestNumber} {ProviderOutcome} for turn {AssistantTurnId}: duration={DurationMs} ms generation={ProviderGenerationId} model={ProviderModel} provider={ProviderName} status={ProviderStatusCode} finish={ProviderFinishReason} input_characters={InputCharacters} output_characters={OutputCharacters} tools_allowed={ToolsAllowed} tool_calls={ToolCalls} usage_received={UsageReceived} prompt_tokens={PromptTokens} completion_tokens={CompletionTokens} reasoning_tokens={ReasoningTokens} received_done={ReceivedDone} request={ProviderRequestId} error_code={ProviderErrorCode} error_type={ProviderErrorType} upstream_code={UpstreamErrorCode} native_finish={ProviderNativeFinishReason} retry_after={RetryAfterSeconds} content_type={ProviderContentType} headers_ms={HeadersDurationMs} first_chunk_ms={FirstChunkDurationMs} last_chunk_ms={LastChunkDurationMs} chunks={ChunkCount} error_details={ProviderErrorDetails} routing={ProviderRoutingDetails} body_state={ProviderErrorBodyState} details_truncated={ProviderDetailsTruncated} details_redacted={ProviderDetailsRedacted} exception_type={ExceptionType} transport_error={TransportError} socket_error={SocketError} request_settings={ProviderRequestSettings}",
            turn.ProviderRequests, outcome, turn.TurnId, duration, GenerationId, Model ?? turn.Model, ProviderName, StatusCode,
            FinishReason, inputCharacters, outputCharacters, allowTools, toolCalls, UsageReceived, PromptTokens, CompletionTokens, ReasoningTokens, receivedDone,
            RequestId, ErrorCode, ErrorType, ProviderErrorCode, NativeFinishReason, RetryAfterSeconds, ContentType, _headersDuration,
            _firstChunkDuration, _lastChunkDuration, _chunks, _errorDetails, _routingDetails, _responseBodyState, _detailsTruncated, _detailsRedacted,
            _exceptionType, _transportError, _socketError, _requestSettings);
        _request = null;
        _apiKey = null;
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

    private string? GetMetadata(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? SafeMetadata(property.GetString()) : null;

    private string? GetCode(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out int value) ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : GetMetadata(element, name);

    private string? GetHeader(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? SafeMetadata(values.FirstOrDefault()) : null;

    private static long? GetTokenCount(JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out long value)
            ? Math.Max(0, value) : null;

    private string? SafeMetadata(string? value)
        => value is { Length: > 0 and <= 128 } && (String.IsNullOrEmpty(_apiKey) || !value.Contains(_apiKey, StringComparison.Ordinal))
            && value.All(character => Char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '/' or '.' or ':' or '~' or ' ')
            ? value : null;
}
