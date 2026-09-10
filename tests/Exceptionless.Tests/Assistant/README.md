# Exie quality evaluations

The assistant quality gate is an opt-in integration test suite that calls the configured AI provider and uses the real Exceptionless HTTP endpoint, authentication, Elasticsearch test data, and MCP tools. It checks the behaviors that have caused the most visible failures:

- a current event is fetched directly without rediscovering its project or stack;
- a project-scoped top-errors question uses one stack search and returns navigable links;
- an organization-wide question lists projects once, stays within the server's project-search limit, and returns navigable links;
- every scenario finishes with non-empty answer text, no streamed error, and no raw DSML tool markup.

The gate makes billable provider requests, so it is skipped by default. Run it before changing the assistant model, system prompt, tool schemas, or tool-selection behavior:

```bash
dotnet build tests/Exceptionless.Tests/Exceptionless.Tests.csproj --maxcpucount:1

RUN_ASSISTANT_EVALS=true \
EX_Assistant__ApiKey='<evaluation-key>' \
dotnet tests/Exceptionless.Tests/bin/Debug/net10.0/Exceptionless.Tests.dll \
  --filter-class 'Exceptionless.Tests.Assistant.AssistantQualityEvaluationTests'
```

Set `EX_Assistant__Model` and `EX_Assistant__Endpoint` to evaluate a candidate model or compatible provider. Use a dedicated provider key with a small monthly hard limit; the tests never print the key.

Each scenario runs independently and authenticates as the seeded organization user, so a failure in one scenario does not prevent the remaining scenarios from exercising the provider.

Exie defaults to the pinned `deepseek/deepseek-v4.1-flash` model. Model changes must preserve these provider contracts:

- Keep streamed reasoning with the assistant's tool calls for subsequent provider requests in the same turn. Prefer structured reasoning blocks so their order, signatures, and opaque data survive. Never send reasoning to the browser or persist it with conversation tool results. See [OpenRouter reasoning preservation](https://openrouter.ai/docs/guides/best-practices/reasoning-tokens#preserving-reasoning).
- Include tool definitions on every request containing tool results. When requesting a final answer after suggestions or tool-budget exhaustion, use `tool_choice: "none"`. See [OpenRouter tool calling](https://openrouter.ai/docs/guides/features/tool-calling).

## Failure diagnostics

The streaming endpoint emits a structured summary for each turn and each provider request. `Exceptionless.Web.Assistant: Information` keeps completed and cancelled summaries visible even when production's default log level is Warning. Failures emit Warning or Error events. The `Exceptionless.Assistant` activity source also exports turn/provider spans; the existing application meter records `ex.assistant.turn.duration`, `ex.assistant.provider.duration`, and `ex.assistant.tool.duration` histograms with bounded outcome/reason tags.

Start with the failed turn in production `ex-prod-app` logs or Exceptionless events from `Exceptionless.Web.Assistant.AssistantService`. Search for its `AssistantTurnId` to collect every provider request and tool failure. Turn summaries include `OrganizationId`, `ConversationId`, `RequestId`, and `TraceId`; provider summaries include `ProviderGenerationId` and `ProviderRequestId` for an OpenRouter investigation. Use `ProviderRequestNumber` to distinguish the initial request, tool continuations, and malformed-response retries.

| Evidence | Fields and interpretation |
| --- | --- |
| Turn outcome | `Outcome`, `FailureReason`, `Stage`, `DurationMs`, `FirstTextDurationMs`. Distinguishes a provider failure, turn deadline, tool exception, response-write failure, and client disconnect. |
| Provider rejection | `ProviderStatusCode` is the HTTP status; `ProviderErrorCode` is the body/stream error code. HTTP 200 can contain a later error with code 429. `ProviderErrorType` is OpenRouter's normalized type; `UpstreamErrorCode` is the provider-specific code. |
| Actual cause | `ProviderErrorDetails` is a JSON string containing selected error messages, parameter locations, nested `metadata.raw` errors, rate-limit source, routing exclusions, and routing funnel steps. |
| Routing | `ProviderModel`, `ProviderName`, and `ProviderRoutingDetails` identify the resolved model, selected provider, and available attempt metadata. Requests opt in with `X-OpenRouter-Metadata: enabled`; OpenRouter may omit metadata for some responses. |
| Request constraints | `ProviderRequestSettings` records output-token limit, temperature, tool choice/count, message count, and provider price caps. `InputCharacters` records request size without content. |
| Stream progress | `HeadersDurationMs`, `FirstChunkDurationMs`, `LastChunkDurationMs`, `ChunkCount`, `OutputCharacters`, `ToolCalls`, `ReceivedDone`, `ProviderFinishReason`, `ProviderNativeFinishReason`, and token counts distinguish a connection failure, interrupted stream, output limit, or reasoning-only response. |
| Transport and retry | `RetryAfterSeconds`, `ProviderContentType`, `ExceptionType`, `TransportError`, and `SocketError` retain response/transport evidence without logging arbitrary exception messages. The turn event also contains `ExceptionStackTrace`. |
| Diagnostic limits | `ProviderErrorBodyState`, `ProviderDetailsTruncated`, and `ProviderDetailsRedacted` explain missing or filtered details. Non-JSON, invalid, empty, or oversized HTTP error bodies are classified without recording their raw content. |

Useful failure reasons include `provider_http_error`, `provider_error`, `provider_transport_error`, `provider_stream_error`, `invalid_provider_response`, `provider_timeout`, `turn_timeout`, `empty_response`, `output_limit`, `content_filter`, `malformed_response`, `tool_round_limit`, `tool_execution_error`, `response_write_error`, `usage_limit`, and `context_limit`. `client_disconnected` is cancellation. A provider warning can precede a recovered turn; assess the final turn outcome as well. Completion records delivery, not whether the answer solved the user's problem; use the quality evaluations above for that.

Error extraction uses an allowlist and removes echoed request values, credentials, and URLs. Prompts, answer text, reasoning, tool arguments/results, flagged input, and arbitrary router pipeline data are not copied into diagnostic events. HTTP error reads are capped at 32,768 characters. Error/routing extraction shares a budget of 64 fields, 16 items per array, six nesting levels, 2,048 characters per text field, and 8,192 total text characters. These bounds are explicit so missing provider evidence is distinguishable from an empty upstream error.

Focused tests cover HTTP and in-stream failures, routing and validation errors, timeouts, cancellation, stream progress, malformed/oversized bodies, correlation, and credential/request redaction. A test also passes the provider failure through the real Serilog Exceptionless sink and serializes the intercepted event to verify that the useful details survive without submitting any event externally.

See OpenRouter's [error semantics](https://openrouter.ai/docs/api_reference/errors-and-debugging) and [router metadata](https://openrouter.ai/docs/guides/features/router-metadata) for upstream field definitions.
