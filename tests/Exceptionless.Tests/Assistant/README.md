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
