using System.Diagnostics;

namespace Exceptionless.Web.Assistant;

internal sealed class AssistantProviderTiming(
    ILogger<AssistantService> logger,
    TimeProvider timeProvider,
    AssistantChatRequest request,
    string model) : IDisposable
{
    private readonly long _started = timeProvider.GetTimestamp();
    private bool _completed;
    private bool _disposed;

    public double ElapsedMilliseconds => timeProvider.GetElapsedTime(_started).TotalMilliseconds;
    public double? HeadersDuration { get; set; }
    public double? FirstChunkDuration { get; set; }
    public string? GenerationId { get; set; }
    public string? ProviderName { get; set; }

    public void Complete()
    {
        _completed = true;
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        double duration = ElapsedMilliseconds;
        AppDiagnostics.AssistantProviderDuration.Record(duration, new KeyValuePair<string, object?>("model", model));
        logger.LogInformation(
            "Assistant provider timing: duration={DurationMs} ms headers={HeadersDurationMs} ms first_chunk={FirstChunkDurationMs} ms stream_completed={ProviderStreamCompleted} model={Model} provider={ProviderName} generation={ProviderGenerationId} organization={OrganizationId} conversation={ConversationId} trace={TraceId}",
            duration, HeadersDuration, FirstChunkDuration, _completed, model, ProviderName, GenerationId,
            request.OrganizationId, request.ConversationId, Activity.Current?.TraceId.ToString());
    }
}
