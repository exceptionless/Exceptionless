---
name: aspire-diagnostics
description: Inspect local Aspire resource health, logs, traces, and browser telemetry.
---

# Aspire Diagnostics

Use runtime evidence to diagnose a local failure or measure an affected operation. Follow root `AGENTS.md` for runtime startup and endpoint scope.

From the repository root, select this AppHost explicitly:

```sh
aspire describe --apphost src/Exceptionless.AppHost --format Json --non-interactive
```

Use the reported resource names and endpoints. Add `--include-hidden` when a resource is not listed.

| Evidence | Command |
| --- | --- |
| Structured logs | `aspire otel logs <resource> --limit 100 --format Json --non-interactive` |
| Process output | `aspire logs <resource> --tail 100 --timestamps --non-interactive` |
| Recent traces | `aspire otel traces --limit 20 --format Json --non-interactive` |
| Trace spans | `aspire otel spans <resource> --trace-id <traceId> --format Json --non-interactive` |
| Export | `aspire export --output ./dogfood-output/aspire-telemetry.zip --non-interactive` |

## Diagnose the affected operation

Start with resource health and the reported failure. Prefer structured logs for application errors and contextual fields; use console logs for startup failures, crashes, and other process output. Use traces when the failure or latency crosses API, queue, job, or storage boundaries, then inspect the relevant spans.

Correlate evidence with the user action, timestamp, resource, and trace ID. For a known trace, inspect related logs with `aspire otel logs --trace-id <traceId> --format Json --non-interactive`. A healthy resource alone does not establish that the operation succeeded.

## Performance investigation

1. Record the app URL, resource names, action, and timestamp; perform the action once.
2. Locate its trace and inspect spans for repeated API requests, Elasticsearch searches, Redis operations, queue publishes, and job processing.
3. Count calls by dependency and operation, identify slow spans, and correlate errors with structured logs. Distinguish retries from duplicate work.
4. Use dashboard metrics for aggregate request counts, error rates, and latency trends. A single trace describes that request, not the workload's overall performance.
5. Export telemetry when a deeper comparison or report needs the evidence preserved.

Report the exact action, resource names, trace IDs, repeated-call counts, durations, and relevant errors. State any missing telemetry that limits the conclusion.

## Browser and environment boundaries

The AppHost enables `WithBrowserLogs()` for frontend resources. Use its dashboard for browser console/network evidence or screenshots when they explain a UI failure, and correlate browser activity with backend traces where available.

Avoid restarting the whole AppHost as a diagnostic loop. For deployed or external environments, follow the user's explicit scope and the platform's diagnostics; do not assume local Aspire commands or endpoints apply.
