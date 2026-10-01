# Assistant tests

Tests for Exie’s access and usage limits, provider streaming, tool handling, model settings, diagnostics, and serialization. Live provider evaluations are opt-in.

Run the backend commands below from the repository root, using the .NET SDK selected by [global.json](../../../global.json).

## Unit tests

```powershell
dotnet test --project tests/Exceptionless.Tests/Exceptionless.Tests.csproj -- `
    --filter-namespace Exceptionless.Tests.Assistant `
    --filter-not-class Exceptionless.Tests.Assistant.AssistantQualityEvaluationTests
```

These tests require neither running infrastructure nor a provider API key. The explicit exclusion keeps live evaluations out of this command even when the evaluation environment variable is set.

For new provider failure cases, use the synthetic streams in [AssistantServiceTests.cs](AssistantServiceTests.cs) and the log, trace, and metric assertions in [AssistantDiagnosticsTests.cs](AssistantDiagnosticsTests.cs).

The runtime model setting is read at the start of every turn, including turns in an existing conversation. Model selection does not impose a provider price filter. Organization token and cost limits still apply using reported provider usage; in-flight reservations and interrupted requests without usage use estimated prices, which may differ from the selected model's prices.

## Provider evaluations

[AssistantQualityEvaluationTests.cs](AssistantQualityEvaluationTests.cs) calls the configured AI provider through the local test HTTP host, authentication, seeded Elasticsearch data, and MCP tools. Docker must be available; [AppWebHostFactory](../AppWebHostFactory.cs) starts the local test infrastructure automatically.

The scenarios cover:

- Investigating the event currently being viewed.
- Finding top errors within one project.
- Finding top errors across an organization’s projects.
- Providing project client setup instructions.

Assertions check tool selection and call limits, expected navigation links, and a completed, nonempty response without streamed errors or raw DSML markup. Passing confirms these scenario assertions; answer quality still needs review when changing the model, system prompt, or tools.

These evaluations make billable provider requests and are disabled by default. Configure `EX_Assistant__ApiKey` in your environment using a dedicated key with a spending limit. Optionally set `EX_Assistant__Model` and `EX_Assistant__Endpoint` to test a different model or compatible provider.

```powershell
$env:RUN_ASSISTANT_EVALS = 'true'
try {
    dotnet test --project tests/Exceptionless.Tests/Exceptionless.Tests.csproj -- `
        --filter-class Exceptionless.Tests.Assistant.AssistantQualityEvaluationTests
} finally {
    Remove-Item Env:RUN_ASSISTANT_EVALS
}
```

## Diagnosing failures

Start with the turn completion log’s `Outcome`, `FailureReason`, and `Stage`. Correlate turn and provider logs using `AssistantTurnId` and `ConversationId`; provider generation IDs identify upstream requests when available. Provider errors can arrive inside an HTTP 200 stream, so inspect the terminal turn outcome when investigating failures.

[AssistantTurnDiagnostics](../../../src/Exceptionless.Web/Assistant/AssistantTurnDiagnostics.cs) and [AssistantProviderDiagnostics](../../../src/Exceptionless.Web/Assistant/AssistantProviderDiagnostics.cs) define the current diagnostic fields and failure classifications.

Server diagnostics do not record prompts, answers, reasoning, or tool payloads directly; provider error messages are retained. Browser prompt and response logging is controlled by conversation sharing preferences, while usage and error diagnostics remain enabled. See [AssistantConversationSharingService](../../../src/Exceptionless.Web/Assistant/AssistantConversationSharingService.cs) and [assistant-telemetry.ts](../../../src/Exceptionless.Web/ClientApp/src/lib/features/assistant/assistant-telemetry.ts) for the implementation.

## Related tests

- [Assistant endpoint tests](../Api/Endpoints/AssistantEndpointTests.cs) cover HTTP authorization and conversation sharing preferences.
- [Admin endpoint tests](../Api/Endpoints/AdminEndpointTests.cs) cover the global sharing default.
- Frontend tests are colocated with the [assistant UI](../../../src/Exceptionless.Web/ClientApp/src/lib/features/assistant). [Session tests](../../../src/Exceptionless.Web/ClientApp/src/lib/features/auth/exceptionless-session.test.ts) and [admin settings tests](../../../src/Exceptionless.Web/ClientApp/src/lib/features/admin/components/assistant-settings.svelte.test.ts) cover the related browser behavior. Run them with the frontend’s `test:unit` script.
