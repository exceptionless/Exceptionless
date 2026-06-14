---
name: dogfood
description: >
  Systematically dogfood Exceptionless locally. Use when asked to dogfood, QA, exploratory test,
  bug hunt, browser-test, or validate frontend/full-stack behavior in this repo. Start from the
  AppHost/Aspire local URLs and choose the Svelte or legacy Angular app based on the changed
  surface. External Exceptionless URLs require an explicit user-provided URL and confirmation.
---

# Dogfood

Systematically explore Exceptionless as a local user, find issues, and produce a concise report with reproducible evidence.

## Setup

Local targets:

- Aspire dashboard: `https://ex.dev.localhost:7101`
- Svelte app: `https://web-ex.dev.localhost:7131/next/`
- Legacy Angular app: `https://angular-ex.dev.localhost:7121`
- API health check: `https://api-ex.dev.localhost:7111/api/v2/about`
- API health fallback for command-line tools with local TLS issues: `http://api-ex.dev.localhost:7110/api/v2/about`

- Start the app with `aspire run` or the AppHost before testing.
- Probe the API health check before browser work.
- Store evidence under `./dogfood-output/` unless the user provides another workspace-local path.
- Use the available browser automation tool for local browser interaction when one is exposed in the session.
- Test localhost by default. Use an external Exceptionless URL only when the user explicitly provides that URL and confirms external testing for the current task.

If the app is not running, start it or report the infrastructure blocker. Do not skip QA just because infrastructure is initially down.

## Workflow

```
1. Health check   Confirm local API responds
2. Initialize     Create output dirs/report
3. Authenticate   Sign in if the tested flow requires it
4. Orient         Capture the starting page and navigation
5. Explore        Walk realistic workflows
6. Document       Save evidence as each issue is found
7. Wrap up        Summarize severity, repro steps, and residual risk
```

## Evidence Rules

- Interactive or stateful bugs need step-by-step repro notes and screenshots; video is useful when timing or multi-step interaction matters.
- Static visible issues, such as clipped text or layout breakage on load, only need a screenshot and the viewport details.
- Capture console/network errors when they explain the user-visible issue.
- Write findings as you discover them so interruptions do not lose context.
- Keep evidence files; do not delete or rewrite history mid-session.

## Report Format

For each finding include:

- Severity: `P0`, `P1`, `P2`, or `P3`
- Area / URL
- Environment: local URL, browser, viewport
- Repro steps
- Expected result
- Actual result
- Evidence file paths
- Notes on console/network errors, if relevant

Prefer 5-10 well-documented issues over a long vague list.

## Scope

Test the changed surface first, then adjacent critical workflows. Never use production data. Production URLs require an explicit user-provided URL and confirmation.
