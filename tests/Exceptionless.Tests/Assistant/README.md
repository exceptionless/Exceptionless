# Exie quality evaluations

The assistant quality gate is an opt-in integration test that calls the configured AI provider and uses the real Exceptionless HTTP endpoint, authentication, Elasticsearch test data, and MCP tools. It checks the behaviors that have caused the most visible failures:

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

## Runtime diagnostics

Every accepted Exie turn emits one structured completion log with `Outcome` (`completed`, `failed`, or `cancelled`), `FailureReason`, `Stage`, duration, time to first answer text, model, provider request count, tool rounds, and tool failures. `AssistantTurnId`, `OrganizationId`, `ConversationId`, `RequestId`, and `TraceId` correlate a turn across logs and traces. These identifiers are also included in the rendered message so Kubernetes console logs remain useful without a structured log viewer.

The `Exceptionless.Web.Assistant` logging override retains Information-level turn and provider summaries even when the production default is Warning. Assistant spans use the dedicated `Exceptionless.Assistant` activity source, leaving the shared Core ingestion activity source unchanged. Failed turns log at Warning; unexpected/provider exceptions log at Error. Client disconnects remain cancellations and log at Information. A `completed` turn means the response finished without a streamed error; it does not establish that the answer was useful or correct. Keep running the quality evaluations above when changing the model, prompt, or tools.

Provider summaries include the generation ID (from `X-Generation-Id` or the stream), resolved model, provider, HTTP status, normalized finish reason, token counts including reasoning tokens when supplied, and whether final usage and `[DONE]` arrived. Thrown transport, parsing, stream, and timeout errors record explicit provider outcomes before propagating to the turn handler. See the [OpenRouter streaming contract](https://openrouter.ai/docs/api/reference/streaming). A request can return HTTP 200 and subsequently fail inside the stream, so HTTP error rates alone do not measure Exie reliability. Generation IDs can be used for provider-side investigation without logging the conversation.

Server diagnostics deliberately exclude prompts, answer text, reasoning text, tool arguments/results, raw provider error bodies, and exception messages. Exception type and stack trace remain available. Tool names and error codes used as metric dimensions come from a fixed allowlist; organization, conversation, generation, and model identifiers are restricted to logs/traces. Browser session events separately capture the user-visible conversation, as described below.

| Failure reason | Investigation |
| --- | --- |
| `turn_timeout` | The shared 120-second turn deadline expired; inspect the last stage, provider duration, and tool progress. |
| `provider_timeout` | A provider operation was cancelled before the shared turn deadline expired. |
| `operation_cancelled` | A non-provider operation was cancelled before the shared turn deadline expired; inspect the stage. |
| `provider_http_error`, `provider_error`, `provider_transport_error`, `provider_stream_error` | Check status, generation ID, provider, and whether the stream completed. |
| `empty_response` | The provider returned neither answer text nor tool calls. |
| `output_limit` | The provider returned no answer text and reported `finish_reason=length`; inspect reasoning and completion tokens before changing budgets. |
| `content_filter` | An empty answer ended with the provider's content-filter finish reason. |
| `malformed_response` | Internal provider markup remained after the existing recovery retry. |
| `tool_round_limit` | The provider continued requesting tools after the final-answer instruction. |
| `usage_limit`, `context_limit` | A turn reached an organization usage limit or the conversation context bound. |
| `invalid_provider_response`, `response_write_error`, `tool_execution_error`, `internal_error` | Inspect the stage, exception type/stack, and correlated trace. |

Returned tool errors are logged with the tool name and error code, even when the model recovers and completes the turn. Thrown tool exceptions also increment tool failures and record duration; interrupted tool invocations record a separate `cancelled` duration with `operation_cancelled`. Provider error objects inside an HTTP 200 stream record `provider_error` even without a finish reason. Provider `output_limit` or `incomplete_stream` warnings can also accompany a completed turn when text was returned. Diagnostics preserve the existing response and accounting behavior; they do not automatically retry tools or change output budgets.

The existing `ex.assistant.turn.outcomes` metric remains unchanged. Additional histograms provide immediate duration and outcome counts:

| Instrument | Dimensions |
| --- | --- |
| `ex.assistant.turn.duration` (ms) | `outcome`, `reason`, `stage` |
| `ex.assistant.provider.duration` (ms) | `outcome` |
| `ex.assistant.tool.duration` (ms) | `tool`, `outcome`, `reason` |

Track the completed share of completed + failed turns, failure counts by reason, cancellation rate separately, and turn/provider latency percentiles. Histogram counts can supply the immediate success-rate denominator; durable usage totals may lag. Failed `assistant.turn` spans carry error status even though the enclosing HTTP response is 200. Traces follow the deployment's existing sampling policy, so use logs and metrics for unsampled failures.

In the hosted Helm deployment, the `ex-prod-app` workload runs `Exceptionless.Web` and serves in-app Exie requests; `ex-prod-api` primarily serves collector API traffic. Start with organization and UTC time, then follow the turn ID and generation IDs. Retained pod logs may not cover replaced pods or old conversations, and counters alone cannot reconstruct a historical failure reason.

The diagnostics and provider-stream tests run locally without Aspire or billable provider requests:

```powershell
dotnet build tests/Exceptionless.Tests/Exceptionless.Tests.csproj --maxcpucount:1
dotnet tests/Exceptionless.Tests/bin/Debug/net10.0/Exceptionless.Tests.dll --filter-namespace Exceptionless.Tests.Assistant --filter-not-class Exceptionless.Tests.Assistant.AssistantQualityEvaluationTests --progress off
```

## Conversation session events

The Svelte app submits Exie events through the existing Exceptionless browser client. They share the signed-in user's session, client configuration, queue, tags, and event exclusions. They go to the app's configured telemetry project. Starting a conversation does not create a separate user session.

Each submitted prompt produces an `assistant.MessageSent` log event. The assembled response produces one `assistant.ResponseCompleted`, `assistant.ResponseFailed`, or `assistant.ResponseCancelled` log event. The log message contains the prompt, answer, or partial answer; a failure without answer text uses the error displayed to the user. Native log summaries make these messages readable in the existing session timeline. Messages are capped at 16,384 characters, with length and truncation metadata. Drafts, individual streamed chunks, reasoning, and raw tool arguments/results are not submitted.

Events carry an `exie` extended-data object with `schema_version: 1`, conversation and message IDs, organization/project context, page path without query/fragment, and page/sheet mode. The conversation ID matches the server diagnostics. Turn summaries also include outcome, elapsed time, time to first text, tool counts/failures, and whether the chat was visible when the turn finished. Retries link the new server conversation back through `previous_conversation_id` and `retry_of_message_id`.

Interactions remain feature usage events:

| Event source | Meaning |
| --- | --- |
| `assistant.Opened`, `assistant.Closed`, `assistant.ViewChanged` | Open, close, or switch between the panel and full page. Closing the panel does not cancel an ongoing response. |
| `assistant.ResponseHelpful`, `assistant.ResponseNotHelpful`, `assistant.ResponseFeedbackCleared` | Explicit feedback linked to the response. |
| `assistant.ResponseRegenerated` | Retry or regenerate, linked to the previous response. |
| `assistant.MessageCopied`, `assistant.SuggestedActionSelected` | Copy a message or act on an Exie suggestion. |
| `assistant.ConversationCleared`, `assistant.ConversationLeft`, `assistant.PageLeft` | Clear, switch organization, unmount, or leave the browser page, with the last outcome/feedback, message count, and whether a turn was still streaming. |

Filter the app telemetry project by `source:assistant.*`, then open an event's session timeline and use the `exie` IDs in event details to follow a conversation. Compare explicit positive/negative feedback, repeated retries, response latency, and departures while waiting. A completed response only means text arrived without an error; it does not prove the answer helped. Copying and continuing are useful signals, while closing the chat alone does not establish frustration.

These browser events are best effort. Page-leave events may be lost during unload, network failure, or a browser crash, and configured client filtering still applies. Use server metrics for operational failure rates; use session events to understand the user journey. A missing terminal event alone is not proof of cancellation or abandonment.

Focused frontend tests exercise the real SDK builders with the queue intercepted, plus streamed success/failure, retries, feedback, context changes, and panel visibility. They do not submit events to a running collector:

```powershell
Set-Location src/Exceptionless.Web/ClientApp
npm run test:unit -- src/lib/features/assistant/assistant-telemetry.test.ts src/lib/features/assistant/components/assistant-panel.svelte.test.ts src/lib/features/assistant/components/assistant-message-actions.svelte.test.ts src/lib/features/auth/exceptionless-session.test.ts
npm run check
```
