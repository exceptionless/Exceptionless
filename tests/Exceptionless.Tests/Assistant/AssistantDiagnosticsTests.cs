using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Web.Api.Endpoints;
using Exceptionless.Web.Assistant;
using Foundatio.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using OpenTelemetry.Trace;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantDiagnosticsTests
{
    [Fact]
    public void AddApm_ExieActivitySource_CreatesExportableSpans()
    {
        // The APM source file is linked into both Web and Job. Select the Web copy
        // explicitly to exercise its real registration without ambiguous type references.
        var assembly = typeof(AssistantService).Assembly;
        var configType = assembly.GetType("OpenTelemetry.ApmConfig", throwOnError: true)!;
        var config = Activator.CreateInstance(configType, new ConfigurationBuilder().Build(), "test", "1.0", false);
        var builder = new HostBuilder();
        assembly.GetType("OpenTelemetry.ApmExtensions", throwOnError: true)!
            .GetMethod("AddApm")!.Invoke(null, [builder, config]);
        using var host = builder.Build();
        _ = host.Services.GetRequiredService<TracerProvider>();

        using var activity = AppDiagnostics.StartActivity("assistant.turn");

        Assert.NotNull(activity);
        Assert.True(activity.IsAllDataRequested);
    }

    [Theory]
    [InlineData("empty_response")]
    [InlineData("output_limit")]
    [InlineData("malformed_response")]
    [InlineData("tool_round_limit")]
    [InlineData("usage_limit")]
    public async Task WriteResponseAsync_StreamedError_RecordsCorrelatedFailureWithoutChangingResponse(string reason)
    {
        var logger = new RecordingAssistantLogger();
        var time = new FakeTimeProvider();
        using var diagnostics = new AssistantTurnDiagnostics(logger, time, "organization-id", "conversation-id", "request-id");
        using var cache = new InMemoryCacheClient();
        var recorder = new RecordingAssistantUsageRecorder();
        var context = CreateHttpContext();
        time.Advance(TimeSpan.FromSeconds(12));

        await AssistantEndpoints.WriteResponseAsync(context,
            StreamEvents([AssistantStreamEvent.Error("private error detail", reason), AssistantStreamEvent.Done()]),
            CreateUsageService(cache, recorder), "organization-id", diagnostics, TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("failed", entry.Properties["Outcome"]);
        Assert.Equal(reason, entry.Properties["FailureReason"]);
        Assert.Equal(12_000d, entry.Properties["DurationMs"]);
        Assert.Equal("organization-id", entry.Properties["OrganizationId"]);
        Assert.Equal("conversation-id", entry.Properties["ConversationId"]);
        Assert.Equal("request-id", entry.Properties["RequestId"]);
        Assert.Equal(diagnostics.TurnId, entry.Properties["AssistantTurnId"]);
        Assert.DoesNotContain("private error detail", entry.Message);
        Assert.Equal(1, Assert.Single(recorder.Records).Increment.Failed);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        var events = await ReadEventsAsync(context);
        Assert.Equal("private error detail", events[0].GetProperty("message").GetString());
        Assert.False(events[0].TryGetProperty("failure_code", out _));
        Assert.False(events[0].TryGetProperty("FailureCode", out _));
        Assert.Equal("done", events[1].GetProperty("type").GetString());
    }

    [Theory]
    [InlineData(false, true, "provider_stream", "failed", "turn_timeout")]
    [InlineData(false, false, "provider_stream", "failed", "provider_timeout")]
    [InlineData(true, true, "provider_stream", "cancelled", "client_disconnected")]
    [InlineData(false, false, "tool_execution", "failed", "operation_cancelled")]
    public async Task WriteResponseAsync_Cancellation_DistinguishesClientAndServer(
        bool clientDisconnected, bool deadlineExpired, string stage, string outcome, string reason)
    {
        var logger = new RecordingAssistantLogger();
        using var diagnostics = new AssistantTurnDiagnostics(logger, TimeProvider.System, "organization-id", "conversation-id", "request-id");
        diagnostics.Stage = stage;
        using var cache = new InMemoryCacheClient();
        var recorder = new RecordingAssistantUsageRecorder();
        var context = CreateHttpContext();
        using var cancellation = new CancellationTokenSource();
        if (deadlineExpired)
            await cancellation.CancelAsync();
        if (clientDisconnected)
            context.RequestAborted = cancellation.Token;

        await AssistantEndpoints.WriteResponseAsync(context,
            StreamEvents([], new OperationCanceledException("private provider detail")),
            CreateUsageService(cache, recorder), "organization-id", diagnostics, cancellation.Token);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(outcome, entry.Properties["Outcome"]);
        Assert.Equal(reason, entry.Properties["FailureReason"]);
        Assert.Equal(stage, entry.Properties["Stage"]);
        Assert.DoesNotContain("private provider detail", entry.Message);
        var usage = Assert.Single(recorder.Records).Increment;
        Assert.Equal(clientDisconnected ? 0 : 1, usage.Failed);
        Assert.Equal(clientDisconnected ? 1 : 0, usage.Cancelled);
        var events = await ReadEventsAsync(context);
        if (clientDisconnected)
            Assert.Empty(events);
        else
            Assert.Equal("Exie took too long to complete this response. Try narrowing the question.", Assert.Single(events).GetProperty("message").GetString());
    }

    [Fact]
    public async Task WriteResponseAsync_ProviderException_RecordsReasonWithoutLoggingProviderMessage()
    {
        var logger = new RecordingAssistantLogger();
        using var diagnostics = new AssistantTurnDiagnostics(logger, TimeProvider.System, "organization-id", "conversation-id", "request-id");
        using var cache = new InMemoryCacheClient();
        var recorder = new RecordingAssistantUsageRecorder();
        var context = CreateHttpContext();
        await AssistantEndpoints.WriteResponseAsync(context,
            StreamEvents([], new AssistantProviderException("private provider detail") { FailureCode = "provider_http_error" }),
            CreateUsageService(cache, recorder), "organization-id", diagnostics, TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("provider_http_error", entry.Properties["FailureReason"]);
        Assert.Equal(typeof(AssistantProviderException).FullName, entry.Properties["ExceptionType"]);
        Assert.DoesNotContain("private provider detail", entry.Message);
        Assert.Equal(1, Assert.Single(recorder.Records).Increment.Failed);
    }

    [Fact]
    public async Task WriteResponseAsync_Success_RecordsCompletionAndFirstTextWithoutLoggingAnswer()
    {
        var logger = new RecordingAssistantLogger();
        var time = new FakeTimeProvider();
        using var diagnostics = new AssistantTurnDiagnostics(logger, time, "organization-id", "conversation-id", "request-id");
        using var cache = new InMemoryCacheClient();
        var recorder = new RecordingAssistantUsageRecorder();
        var context = CreateHttpContext();
        time.Advance(TimeSpan.FromSeconds(3));
        await AssistantEndpoints.WriteResponseAsync(context,
            StreamEvents([AssistantStreamEvent.TextDelta("private answer"), AssistantStreamEvent.Done()]),
            CreateUsageService(cache, recorder), "organization-id", diagnostics, TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("completed", entry.Properties["Outcome"]);
        Assert.Equal("none", entry.Properties["FailureReason"]);
        Assert.Equal(3000d, entry.Properties["FirstTextDurationMs"]);
        Assert.DoesNotContain("private answer", entry.Message);
        Assert.Equal(1, Assert.Single(recorder.Records).Increment.Completed);
    }

    [Fact]
    public void Finish_FailedTurn_RecordsErrorSpanAndBoundedMetricTagsOnce()
    {
        var activities = new ConcurrentQueue<Activity>();
        var activitySource = AppDiagnostics.ActivitySource;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source == activitySource,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);
        var measurements = new List<Dictionary<string, object?>>();
        string? turnId = null;
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Name == "ex.assistant.turn.duration")
                    current.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            if (Activity.Current?.GetTagItem("assistant.turn.id") as string == turnId)
                measurements.Add(tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value));
        });
        meterListener.Start();
        var logger = new RecordingAssistantLogger();
        using (var diagnostics = new AssistantTurnDiagnostics(logger, TimeProvider.System, "organization-id", "conversation-id", "request-id"))
        {
            turnId = diagnostics.TurnId;
            diagnostics.Stage = "provider_stream";
            diagnostics.Finish("failed", "turn_timeout");
            diagnostics.Finish("completed");
        }

        var measurement = Assert.Single(measurements);
        Assert.Equal(3, measurement.Count);
        Assert.Equal("failed", measurement["outcome"]);
        Assert.Equal("turn_timeout", measurement["reason"]);
        Assert.Equal("provider_stream", measurement["stage"]);
        var activity = Assert.Single(activities, activity => activity.GetTagItem("assistant.turn.id") as string == turnId);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("turn_timeout", activity.StatusDescription);
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void ObserveChunk_OutputLimit_RecordsGenerationAndReasoningWithoutContent()
    {
        var logger = new RecordingAssistantLogger();
        using var diagnostics = new AssistantTurnDiagnostics(logger, TimeProvider.System, "organization-id", "conversation-id", "request-id");
        diagnostics.Model = "configured-model";
        using var provider = diagnostics.StartProviderRequest(1000, true, TestContext.Current.CancellationToken);
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("X-Generation-Id", "gen-header");
        provider.ObserveResponse(response);
        using var document = JsonDocument.Parse("""
            {"id":"gen-stream","model":"resolved-model","provider":"Example Provider",
             "choices":[{"delta":{"reasoning":"private reasoning"},"finish_reason":"length"}],
             "usage":{"prompt_tokens":100,"completion_tokens":2048,"completion_tokens_details":{"reasoning_tokens":2048}}}
            """);
        provider.ObserveChunk(document.RootElement);
        provider.Complete(0, 0, true);

        Assert.Equal("gen-stream", provider.GenerationId);
        Assert.Equal("length", provider.FinishReason);
        Assert.Equal(2048, provider.ReasoningTokens);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("output_limit", entry.Properties["ProviderOutcome"]);
        Assert.Equal("resolved-model", entry.Properties["ProviderModel"]);
        Assert.Equal(100L, entry.Properties["PromptTokens"]);
        Assert.Equal(2048L, entry.Properties["CompletionTokens"]);
        Assert.DoesNotContain("private reasoning", entry.Message);
    }

    [Fact]
    public void ObserveChunk_UnexpectedMetadata_DoesNotThrowOrLogUnboundedValues()
    {
        var logger = new RecordingAssistantLogger();
        using var diagnostics = new AssistantTurnDiagnostics(logger, TimeProvider.System, "organization-id", "conversation-id", "request-id");
        using var provider = diagnostics.StartProviderRequest(1000, true, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse("""
            {"id":"private\nvalue","model":{"unexpected":true},"provider":null,
             "choices":[{"finish_reason":"unexpected-provider-string"}],
             "usage":{"completion_tokens_details":{"reasoning_tokens":"not-a-number"}}}
            """);
        provider.ObserveChunk(document.RootElement);
        provider.Complete(10, 0, true);

        Assert.Null(provider.GenerationId);
        Assert.Null(provider.Model);
        Assert.Null(provider.ReasoningTokens);
        Assert.Equal("unknown", provider.FinishReason);
        Assert.DoesNotContain("private", Assert.Single(logger.Entries).Message);
    }

    [Fact]
    public void RecordToolResult_FailedTool_RecordsCodeWithoutArgumentsOrResult()
    {
        var logger = new RecordingAssistantLogger();
        using var diagnostics = new AssistantTurnDiagnostics(logger, TimeProvider.System, "organization-id", "conversation-id", "request-id");
        diagnostics.StartTool("search_stacks");
        diagnostics.RecordToolResult("""{"ok":false,"error":{"code":"invalid_filter","message":"private filter"}}""", 100);
        diagnostics.StartTool("private-hallucinated-tool-name");
        diagnostics.RecordToolResult("""{"ok":false,"error":{"code":"private-error-code","message":"private details"}}""", 100);
        diagnostics.Finish("completed");

        Assert.Equal(2, diagnostics.ToolFailures);
        Assert.Equal("unknown", diagnostics.LastTool);
        Assert.Equal("tool_error", diagnostics.LastToolError);
        Assert.Equal("invalid_filter", logger.Entries[0].Properties["ToolErrorCode"]);
        Assert.All(logger.Entries, entry => Assert.DoesNotContain("private", entry.Message));
        Assert.Equal("completed", logger.Entries[^1].Properties["Outcome"]);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    public void RecordToolResult_NonObjectResult_DoesNotInterruptTheTurn(string result)
    {
        var logger = new RecordingAssistantLogger();
        using var diagnostics = new AssistantTurnDiagnostics(logger, TimeProvider.System, "organization-id", "conversation-id", "request-id");
        diagnostics.StartTool("get_event");
        diagnostics.RecordToolResult(result, 100);

        Assert.Equal(0, diagnostics.ToolFailures);
        Assert.Empty(logger.Entries);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement[]> ReadEventsAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        string content = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        return content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.Clone();
        }).ToArray();
    }

    private static AssistantUsageService CreateUsageService(ICacheClient cache, RecordingAssistantUsageRecorder recorder)
    {
        var options = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BaseURL"] = "https://localhost" })
            .Build());
        return new AssistantUsageService(cache, null!, recorder, options, TimeProvider.System, NullLogger<AssistantUsageService>.Instance);
    }

    private static async IAsyncEnumerable<AssistantStreamEvent> StreamEvents(AssistantStreamEvent[] events, Exception? exception = null)
    {
        await Task.Yield();
        if (exception is not null)
            throw exception;
        foreach (var item in events)
            yield return item;
    }
}

internal sealed class RecordingAssistantLogger : ILogger<AssistantService>
{
    public List<AssistantLogEntry> Entries { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add(new AssistantLogEntry(logLevel, formatter(state, exception),
            ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(property => property.Key, property => property.Value)));
}

internal sealed record AssistantLogEntry(LogLevel Level, string Message, Dictionary<string, object?> Properties);
