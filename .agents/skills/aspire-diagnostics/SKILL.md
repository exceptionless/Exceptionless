---
name: aspire-diagnostics
description: >
  Use when investigating a running Exceptionless Aspire app through resource state, logs,
  OpenTelemetry logs/traces/spans, metrics, browser telemetry, dashboard data, or telemetry
  export. Based on Microsoft's official aspire-monitoring workflow skill. Do not use for ordinary
  code reading, narrow edits, or normal build/test work.
---

# Aspire Diagnostics

Use Aspire diagnostics when local behavior is unclear and runtime evidence will help. Keep normal edit/build/test work out of this skill.

Basis: Microsoft's official Aspire skills split monitoring into `aspire-monitoring`, which is for resource state, logs, traces, metrics, browser telemetry, and dashboard data. Lifecycle/start/stop belongs to orchestration; AppHost authoring belongs to backend/AppHost work.

## Local Targets

- Aspire dashboard: `https://ex.dev.localhost:7101`
- Svelte app: `https://web-ex.dev.localhost:7131/next/`
- Legacy Angular app: `https://angular-ex.dev.localhost:7121`
- API health: `https://api-ex.dev.localhost:7111/api/v2/about`
- API health fallback for command-line tools with local TLS issues: `http://api-ex.dev.localhost:7110/api/v2/about`

## When To Use

- A local request, browser flow, job, queue, Redis, Elasticsearch, or WebSocket behavior is failing.
- You need logs or traces before deciding whether code should change.
- A frontend issue may need browser console/network evidence from Aspire browser telemetry.
- A resource appears unhealthy, missing, stuck, or wired to the wrong endpoint.
- You need lightweight local performance evidence: how many API, Redis, Elasticsearch, queue, or job calls one app action produces, and where time is spent.
- You need a telemetry export for a deeper report.

Do not use this skill just because a file changed.

## Workflow

1. Check resource state first: `aspire describe --format Json --non-interactive`; use the endpoints Aspire reports instead of guessing ports.
2. If a resource is missing, retry with `--include-hidden`.
3. Inspect structured logs before console logs: `aspire otel logs <resource> --limit 100 --format Json --non-interactive`.
4. Use console logs for process output: `aspire logs <resource> --tail 100 --timestamps --non-interactive`.
5. Use traces for cross-resource failures or latency: `aspire otel traces --limit 20 --format Json --non-interactive`.
6. Use spans for a known trace: `aspire otel spans <resource> --trace-id <traceId> --format Json --non-interactive`.
7. Export evidence when useful: `aspire export --output .\dogfood-output\aspire-telemetry.zip --non-interactive`.

Prefer `--format Json` when the output will be parsed or summarized. Include exact resource names, trace IDs, error snippets, and dashboard/resource evidence in the report.

## Performance Pass

Use Aspire traces/logs/dashboard metrics to measure one user action before optimizing it.

1. Start from a clean moment: note the app URL, resource names, user action, and timestamp.
2. Perform the action once in the local app.
3. Pull recent traces: `aspire otel traces --limit 50 --format Json --non-interactive`.
4. Pick the trace for the action and inspect spans: `aspire otel spans --trace-id <traceId> --format Json --non-interactive`.
5. Count calls by dependency/resource/path, especially API requests, Elasticsearch searches, Redis operations, queue publishes, and repeated frontend API calls.
6. Check related structured logs: `aspire otel logs --trace-id <traceId> --format Json --non-interactive`.
7. Use dashboard metrics and telemetry export when aggregate request counts, error rates, or latency trends matter.

Report counts and evidence, not impressions: total spans, repeated calls, slowest spans, resource names, trace ID, and the exact action tested.

## Browser Telemetry

This repo's AppHost enables `WithBrowserLogs()` for both frontend resources. Use the Aspire dashboard when browser console logs, network requests, or screenshots would explain a frontend failure.

## Guardrails

- Investigate before editing code.
- Do not repeatedly restart the whole AppHost. If lifecycle work is needed, use the normal Aspire/AppHost guidance and state why.
- Use `--apphost src/Exceptionless.AppHost` when multiple AppHosts or worktrees make the target ambiguous.
- For deployed or external environments, do not use local Aspire CLI assumptions. Get explicit user scope and use the platform's diagnostics.
