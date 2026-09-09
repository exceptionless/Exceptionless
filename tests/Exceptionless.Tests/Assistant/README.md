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

Provider summaries include the generation ID (from `X-Generation-Id` or the stream), resolved model, provider, HTTP status, normalized finish reason, token counts including reasoning tokens when supplied, and whether final usage and `[DONE]` arrived. Thrown transport, parsing, stream, and timeout errors record explicit provider outcomes before propagating to the turn handler. Provider outcomes distinguish browser disconnects (`cancelled`), the shared turn deadline (`turn_timeout`), and provider-only cancellation (`provider_timeout`). See the [OpenRouter streaming contract](https://openrouter.ai/docs/api/reference/streaming). A request can return HTTP 200 and subsequently fail inside the stream, so HTTP error rates alone do not measure Exie reliability. Generation IDs can be used for provider-side investigation without logging the conversation.

Server diagnostics deliberately exclude prompts, answer text, reasoning text, tool arguments/results, raw provider error bodies, and exception messages. Exception type and stack trace remain available. Tool names and error codes used as metric dimensions come from a fixed allowlist; organization, conversation, generation, and model identifiers are restricted to logs/traces. Browser session events record usage and error details, with optional conversation logging controlled by the global admin setting below.

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

Returned tool errors are logged with the tool name and error code, even when the model recovers and completes the turn. Thrown tool exceptions and deadlines also increment tool failures and record duration; browser disconnects record a separate `cancelled` duration with `client_disconnected`. Tool cancellation reasons distinguish the shared deadline (`turn_timeout`) from other operation cancellation (`operation_cancelled`). Provider error objects inside an HTTP 200 stream record `provider_error` even without a finish reason. Provider `output_limit` or `incomplete_stream` warnings can also accompany a completed turn when text was returned. Diagnostics preserve the existing response and accounting behavior; they do not automatically retry tools or change output budgets.

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

Each submitted prompt produces an `assistant.MessageSent` feature usage event. A turn produces one `assistant.ResponseCompleted`, `assistant.ResponseFailed`, or `assistant.ResponseCancelled` feature usage event. These events record character counts and outcomes. Failure events retain the error displayed to the user in `error_message`, capped at 2,048 characters for diagnosis. Existing application error collection is unchanged.

Global admins control **Conversation sharing default** beside Exie availability and model settings. It defaults to off for new and legacy settings records. Enable it during the early rollout through the settings page or `PUT /api/v2/admin/assistant-settings/conversation-sharing` with `{ "enabled": true }`; set it to false to stop sharing by default later.

Users see a compact **Chat sharing: On/Off** control beside the composer disclaimer. It opens a popover where they can change sharing for their account across conversations and devices. `GET /api/v2/assistant/conversation-sharing` returns the effective setting, default, and whether the user has chosen. The authenticated user's `PUT` at the same route saves `{ "enabled": true }` or `{ "enabled": false }`; `{ "enabled": null }` restores the default. Existing users have no override and follow the admin default. Explicit choices always win, even when originally saved equal to the default. Enabling the default therefore uses opt-out sharing; the visible control identifies inherited defaults and allows an immediate change.

Each accepted turn reads the saved user choice and returns `X-Exie-Full-Logging`. Transcript capture requires both an explicit true header and an enabled, loaded sharing control. Turning sharing off discards the active reply buffer and prevents subsequent transcript capture; already submitted events are retained. If saving an opt-out fails, capture stays paused on the current page and the UI asks the user to retry saving for other devices. Changes elsewhere are read for the next turn; a reply already in progress on another device retains the choice selected at its start. Usage, feedback, and error diagnostics continue regardless of sharing.

When enabled, full logging adds an `assistant.Prompt` log event and one assembled `assistant.Response` log event in the existing session. Each message is capped at 16,384 characters, with original character count and truncation metadata. When disabled, prompt and response text is not copied into telemetry. Drafts, individual streamed chunks, reasoning, raw tool arguments/results, and generated suggestion labels/destinations are never submitted. Suggestion events record only whether the action navigated or submitted a prompt.

Events carry an `exie` extended-data object with `schema_version: 1`, conversation and message IDs, organization/project context, page path without query/fragment, and page/sheet mode. The conversation ID matches the server diagnostics. Turn summaries also include outcome, elapsed time, time to first text, tool counts/failures, and whether the chat was visible when the turn finished. Retries link the new server conversation back through `previous_conversation_id` and `retry_of_message_id`.

Other feature usage events describe interactions:

| Event source | Meaning |
| --- | --- |
| `assistant.Opened`, `assistant.Closed`, `assistant.ViewChanged` | Open, close, or switch between the panel and full page. Closing the panel does not cancel an ongoing response. |
| `assistant.ResponseHelpful`, `assistant.ResponseNotHelpful`, `assistant.ResponseFeedbackCleared` | Explicit feedback linked to the response. |
| `assistant.ResponseRegenerated` | Retry or regenerate, linked to the previous response. |
| `assistant.MessageCopied`, `assistant.SuggestedActionSelected` | Copy a message or act on an Exie suggestion. |
| `assistant.ConversationCleared`, `assistant.ConversationLeft`, `assistant.PageLeft` | Clear, switch organization, unmount, or leave the browser page, with the last outcome/feedback, message count, and whether a turn was still streaming. |

Filter the app telemetry project by `source:assistant.*`, then open an event's session timeline and use the `exie` IDs in event details to follow a conversation. Use `type:usage` when counting turns or interactions so optional transcript logs do not inflate the counts. Compare explicit positive/negative feedback, repeated retries, response latency, and departures while waiting. A completed response only means text arrived without an error; it does not prove the answer helped. Copying and continuing are useful signals, while closing the chat alone does not establish frustration.

These browser events are best effort. Page-leave events may be lost during unload, network failure, or a browser crash, and configured client filtering still applies. Use server metrics for operational failure rates; use session events to understand the user journey. A missing terminal event alone is not proof of cancellation or abandonment.

Focused frontend tests exercise the real SDK builders with the queue intercepted, verify transcript capture is controlled by the server flag, and cover settings changes, streamed success/failure, retries, feedback, context changes, and panel visibility. They do not submit events to a running collector:

```powershell
Set-Location src/Exceptionless.Web/ClientApp
npm run test:unit -- src/lib/features/assistant/assistant-telemetry.test.ts src/lib/features/assistant/components/assistant-panel.svelte.test.ts src/lib/features/assistant/components/assistant-message-actions.svelte.test.ts src/lib/features/auth/exceptionless-session.test.ts src/lib/features/admin/components/assistant-settings.svelte.test.ts
npm run check
```
