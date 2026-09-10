using System.Net;
using System.Text;
using System.Text.Json;
using Exceptionless.Core;
using Exceptionless.Web.Assistant;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Exceptionless.Tests.Assistant;

public sealed partial class AssistantServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamAsync_UpstreamError_PreservesCauseAndCorrelationWithoutRequestContent(bool streaming)
    {
        const string privatePrompt = "private-prompt-canary";
        const string apiKey = "api-key-canary";
        string error = JsonSerializer.Serialize(new
        {
            id = "gen-failed-request",
            model = "resolved-model",
            provider = "Fireworks",
            error = new
            {
                code = 429,
                message = "Provider returned error",
                metadata = new
                {
                    error_type = "rate_limit_exceeded",
                    provider_code = "invalid_request_error",
                    limit_source = "upstream_provider_shared_pool",
                    raw = JsonSerializer.Serialize(new
                    {
                        error = new
                        {
                            type = "invalid_request_error",
                            message = $"Missing reasoning_content at messages[2]. Input: {privatePrompt}. Authorization: Bearer {apiKey}",
                            param = "messages[2].reasoning_content"
                        },
                        request = new { messages = new[] { new { content = "unrelated-private-content-canary" } } }
                    }),
                    flagged_input = "flagged-content-canary"
                }
            },
            choices = new[] { new { delta = new { content = "" }, finish_reason = "error", native_finish_reason = "upstream_error" } },
            openrouter_metadata = new
            {
                attempt = 2,
                strategy = "fallback",
                attempts = new[] { new { provider = "Fireworks", model = "resolved-model", status = 429 } },
                pipeline = new[] { new { data = new { prompt = "pipeline-content-canary" } } }
            }
        });
        var handler = new ProviderErrorHandler(streaming, error);
        var options = AppOptions.ReadFromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BaseURL"] = "https://localhost", ["Assistant:ApiKey"] = apiKey })
            .Build());
        var logger = new RecordingAssistantLogger();
        using var diagnostics = new AssistantTurnDiagnostics(logger, TimeProvider.System, "organization-id", "conversation-id", "request-id");
        var service = CreateAssistantService(handler, options, logger: logger);

        var exception = await Assert.ThrowsAsync<AssistantProviderException>(async () =>
        {
            await foreach (var _ in service.StreamAsync(
                new AssistantChatRequest([new AssistantChatMessage("user", privatePrompt)], OrganizationId: "organization-id"),
                "user-id", CreatePlanOptions(), diagnostics, TestContext.Current.CancellationToken))
            {
            }
        });
        diagnostics.Finish("failed", exception.FailureCode, exception);

        var entry = Assert.Single(logger.Entries, entry => entry.Properties.ContainsKey("ProviderOutcome"));
        Assert.Equal(streaming ? 200 : 429, entry.Properties["ProviderStatusCode"]);
        Assert.Equal("429", entry.Properties["ProviderErrorCode"]);
        Assert.Equal("rate_limit_exceeded", entry.Properties["ProviderErrorType"]);
        Assert.Equal("invalid_request_error", entry.Properties["UpstreamErrorCode"]);
        Assert.Equal("gen-failed-request", entry.Properties["ProviderGenerationId"]);
        Assert.Equal("request-provider-id", entry.Properties["ProviderRequestId"]);
        Assert.Equal(30d, entry.Properties["RetryAfterSeconds"]);
        Assert.Equal("Fireworks", entry.Properties["ProviderName"]);
        Assert.Equal(diagnostics.TurnId, entry.Properties["AssistantTurnId"]);
        Assert.Equal(streaming ? "upstream_error" : null, entry.Properties["ProviderNativeFinishReason"]);
        Assert.False(Assert.IsType<bool>(entry.Properties["ReceivedDone"]));
        Assert.True(Assert.IsType<bool>(entry.Properties["ProviderDetailsRedacted"]));
        Assert.Contains("Missing reasoning_content at messages[2]", Assert.IsType<string>(entry.Properties["ProviderErrorDetails"]));
        Assert.Contains("upstream_provider_shared_pool", Assert.IsType<string>(entry.Properties["ProviderErrorDetails"]));
        Assert.Contains("fallback", Assert.IsType<string>(entry.Properties["ProviderRoutingDetails"]));
        using var settings = JsonDocument.Parse(Assert.IsType<string>(entry.Properties["ProviderRequestSettings"]));
        Assert.Equal(AssistantLimits.MaximumOutputTokens, settings.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("auto", settings.RootElement.GetProperty("tool_choice").GetString());
        Assert.True(settings.RootElement.GetProperty("tool_count").GetInt32() > 0);
        Assert.True(settings.RootElement.GetProperty("message_count").GetInt32() > 0);
        Assert.Equal("conversation-id", logger.Entries[^1].Properties["ConversationId"]);

        string rendered = String.Join('\n', logger.Entries.Select(entry => entry.Message));
        Assert.DoesNotContain("canary", rendered);
        Assert.Equal("enabled", handler.RouterMetadataHeader);
    }

    private sealed class ProviderErrorHandler(bool streaming, string error) : HttpMessageHandler
    {
        public string? RouterMetadataHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RouterMetadataHeader = request.Headers.GetValues("X-OpenRouter-Metadata").Single();
            var response = new HttpResponseMessage(streaming ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(streaming ? $"data: {error}\n\n" : error, Encoding.UTF8,
                    streaming ? "text/event-stream" : "application/json")
            };
            response.Headers.Add("X-Request-Id", "request-provider-id");
            response.Headers.Add("X-Generation-Id", "gen-header-id");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return Task.FromResult(response);
        }
    }
}
