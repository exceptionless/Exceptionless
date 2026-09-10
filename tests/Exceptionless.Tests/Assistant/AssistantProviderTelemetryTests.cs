using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Exceptionless.Insulation.Security;
using Exceptionless.Models;
using Exceptionless.Serializer;
using Exceptionless.Web.Assistant;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Sinks.Exceptionless;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed class AssistantProviderTelemetryTests
{
    [Fact]
    public void Capture_ValidationError_PreservesParameterLocationWithoutInput()
    {
        using var error = JsonDocument.Parse("""
            {"detail":[{"type":"missing","loc":["body","messages",2,"reasoning_content"],
              "msg":"Field required","input":{"content":"private-input-canary"}}]}
            """);
        var sanitizer = new AssistantProviderErrorDetails(default, null);

        using var captured = JsonDocument.Parse(JsonSerializer.Serialize(sanitizer.Capture(error.RootElement)));
        var detail = captured.RootElement.GetProperty("detail")[0];
        Assert.Equal(2, detail.GetProperty("loc")[2].GetInt32());
        Assert.Equal("reasoning_content", detail.GetProperty("loc")[3].GetString());
        Assert.Equal("Field required", detail.GetProperty("msg").GetString());
        Assert.False(detail.TryGetProperty("input", out _));
    }

    [Fact]
    public void ObserveChunk_RoutingRestriction_RecordsExclusionReasonsAndLegacyProviderCode()
    {
        var logger = new RecordingAssistantLogger();
        using var turn = new AssistantTurnDiagnostics(logger, TimeProvider.System, "organization-id", "conversation-id", "request-id");
        using var provider = turn.StartProviderRequest(100, true, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse("""
            {"error":{"code":"404","message":"No eligible providers","metadata":{
              "provider_error_code":"no_endpoints","input_endpoint_count":2,
              "failed_routing_step":"Filter by Guardrails",
              "ineligibility_reasons":[{"reason":"paid-model-training-violation-by-account","endpoint_count":2}],
              "routing_funnel":[{"step":"Initial Endpoints","endpoint_count":7}]
            }}}
            """);
        provider.ObserveChunk(document.RootElement);
        provider.RecordException(new AssistantProviderException("Provider returned error"));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("404", entry.Properties["ProviderErrorCode"]);
        Assert.Equal("no_endpoints", entry.Properties["UpstreamErrorCode"]);
        string details = Assert.IsType<string>(entry.Properties["ProviderErrorDetails"]);
        Assert.Contains("paid-model-training-violation-by-account", details);
        Assert.Contains("Filter by Guardrails", details);
        Assert.Contains("Initial Endpoints", details);
    }

    [Theory]
    [InlineData("<html>private-body-canary</html>", "invalid_json")]
    [InlineData("{\"error\":\"private-body-canary", "invalid_json")]
    [InlineData("[]", "invalid_shape")]
    [InlineData("", "empty")]
    public async Task ObserveErrorResponseAsync_InvalidBody_RetainsStatusWithoutLeakingBody(string body, string expectedState)
    {
        var logger = new RecordingAssistantLogger();
        using var turn = new AssistantTurnDiagnostics(logger, TimeProvider.System, "organization-id", "conversation-id", "request-id");
        using var provider = turn.StartProviderRequest(100, true, TestContext.Current.CancellationToken);
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent(body) };
        provider.ObserveResponse(response);
        await provider.ObserveErrorResponseAsync(response, TestContext.Current.CancellationToken);
        provider.RecordException(new AssistantProviderException("Rejected") { FailureCode = "provider_http_error" });

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(502, entry.Properties["ProviderStatusCode"]);
        Assert.Equal(expectedState, entry.Properties["ProviderErrorBodyState"]);
        Assert.DoesNotContain("private-body-canary", entry.Message);
    }

    [Fact]
    public async Task ObserveErrorResponseAsync_OversizedBody_ReportsTruncation()
    {
        var logger = new RecordingAssistantLogger();
        using var turn = new AssistantTurnDiagnostics(logger, TimeProvider.System, "organization-id", "conversation-id", "request-id");
        using var provider = turn.StartProviderRequest(100, true, TestContext.Current.CancellationToken);
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent(new string('x', 100_000)) };
        provider.ObserveResponse(response);
        await provider.ObserveErrorResponseAsync(response, TestContext.Current.CancellationToken);
        provider.RecordException(new AssistantProviderException("Rejected") { FailureCode = "provider_http_error" });

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("truncated", entry.Properties["ProviderErrorBodyState"]);
        Assert.Equal(true, entry.Properties["ProviderDetailsTruncated"]);
        Assert.Null(entry.Properties["ProviderErrorDetails"]);
    }

    [Fact]
    public void ObserveChunk_StreamFailure_RecordsProgressAndTiming()
    {
        var logger = new RecordingAssistantLogger();
        var time = new FakeTimeProvider();
        using var turn = new AssistantTurnDiagnostics(logger, time, "organization-id", "conversation-id", "request-id");
        using var provider = turn.StartProviderRequest(100, true, TestContext.Current.CancellationToken);
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        time.Advance(TimeSpan.FromSeconds(1));
        provider.ObserveResponse(response);
        using var chunk = JsonDocument.Parse("""{"id":"gen-progress","choices":[{"delta":{"content":"private output"}}]}""");
        time.Advance(TimeSpan.FromSeconds(2));
        provider.ObserveChunk(chunk.RootElement);
        time.Advance(TimeSpan.FromSeconds(7));
        provider.RecordException(new IOException("private exception message"), 14, 1);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(1000d, entry.Properties["HeadersDurationMs"]);
        Assert.Equal(3000d, entry.Properties["FirstChunkDurationMs"]);
        Assert.Equal(3000d, entry.Properties["LastChunkDurationMs"]);
        Assert.Equal(10_000d, entry.Properties["DurationMs"]);
        Assert.Equal(1, entry.Properties["ChunkCount"]);
        Assert.Equal(14, entry.Properties["OutputCharacters"]);
        Assert.Equal(1, entry.Properties["ToolCalls"]);
        Assert.False(Assert.IsType<bool>(entry.Properties["ReceivedDone"]));
        Assert.DoesNotContain("private", entry.Message);
    }

    [Fact]
    public void Capture_ErrorMessages_RedactsRequestValuesAndBoundsText()
    {
        using var request = JsonDocument.Parse("""
            [{"role":"user","content":"private-prompt-canary"},
             {"role":"assistant","reasoning":"private-reasoning-canary","tool_calls":[{"function":{"arguments":"{\"filter\":\"private-filter-canary\"}"}}]},
             {"role":"tool","content":"{\"message\":\"private-result-canary\"}"}]
            """);
        using var error = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            message = "Missing reasoning_content. private-prompt-canary private-reasoning-canary private-filter-canary private-result-canary sk-or-v1-provider-key-canary api-key-canary https://example.test?token=canary",
            metadata = new { raw = new { error = new { message = new string('x', 10_000) } } },
            messages = new[] { new { content = "other-prompt-canary" } }
        }));
        var sanitizer = new AssistantProviderErrorDetails(request.RootElement, "api-key-canary");
        string result = JsonSerializer.Serialize(sanitizer.Capture(error.RootElement));

        Assert.Contains("Missing reasoning_content", result);
        Assert.DoesNotContain("canary", result);
        Assert.True(sanitizer.Redacted);
        Assert.True(sanitizer.Truncated);
        Assert.True(result.Length < 4096);
    }

    [Fact]
    public void RecordException_ExceptionlessSink_PreservesDiagnosticFieldsAndOmitsSecrets()
    {
        Event? submittedEvent = null;
        using var client = new ExceptionlessClient(configuration =>
        {
            configuration.ApiKey = "00000000000000000000000000000000";
            configuration.UseInMemoryStorage();
        });
        client.SubmittingEvent += (_, args) =>
        {
            submittedEvent = args.Event;
            args.Cancel = true;
        };
        using var serilog = new LoggerConfiguration()
            .ApplySensitiveDataLogging()
            .WriteTo.Sink(new ExceptionlessSink(client: client))
            .CreateLogger();
        using var loggerFactory = new SerilogLoggerFactory(serilog, dispose: false);
        using var parent = new Activity("http-request").Start();
        using var turn = new AssistantTurnDiagnostics(loggerFactory.CreateLogger<AssistantService>(), TimeProvider.System,
            "organization-id", "conversation-id", "request-id");
        using var provider = turn.StartProviderRequest(100, true, TestContext.Current.CancellationToken);
        provider.ObserveRequest(new Dictionary<string, object?>
        {
            ["messages"] = new[] { new { role = "user", content = "private-prompt-canary" } }
        }, "private-api-key-canary");
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        provider.ObserveResponse(response);
        using var document = JsonDocument.Parse("""
            {"id":"gen-sink-test","provider":"Fireworks","error":{"code":429,"message":"Provider returned error","metadata":{
             "error_type":"rate_limit_exceeded","provider_code":"rate_limited",
             "raw":{"error":{"message":"Missing reasoning_content: private-prompt-canary private-api-key-canary"},"request":{"content":"other-private-canary"}}
            }}}
            """);
        provider.ObserveChunk(document.RootElement);
        provider.RecordException(new AssistantProviderException("private-exception-canary"));

        Assert.NotNull(submittedEvent);
        Assert.Contains("ProviderErrorDetails", submittedEvent.Data.Keys);
        Assert.Contains("ProviderErrorCode", submittedEvent.Data.Keys);
        Assert.Contains("AssistantTurnId", submittedEvent.Data.Keys);
        string serialized = new DefaultJsonSerializer().Serialize(submittedEvent);
        Assert.Contains("ProviderErrorDetails", serialized);
        Assert.Contains("ProviderErrorCode", serialized);
        Assert.Contains("rate_limit_exceeded", serialized);
        Assert.Contains("Missing reasoning_content", serialized);
        Assert.Contains("gen-sink-test", serialized);
        Assert.Contains(turn.TurnId, serialized);
        Assert.DoesNotContain("canary", serialized);
    }
}
