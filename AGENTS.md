# Exceptionless

Real-time error monitoring platform handling billions of requests (ASP.NET Core 10 + Svelte 5). Act as a distinguished engineer focusing on readability, performance while maintaining backwards compatibility.

## Quick Start

Start everything with the Aspire CLI: `aspire run --project src/Exceptionless.AppHost`. This launches all services (Elasticsearch, Redis, API, Job worker) and opens the Aspire dashboard. Alternatively, run `Exceptionless.AppHost` directly from your IDE.

## Build & Test

| Task           | Command                                                         |
| -------------- | --------------------------------------------------------------- |
| Run (Aspire)   | `aspire run --project src/Exceptionless.AppHost`                |
| Backend build  | `dotnet build`                                                  |
| Backend test   | `dotnet test`                                                   |
| Frontend build | `cd src/Exceptionless.Web/ClientApp && npm ci && npm run build` |
| Frontend test  | `npm run test:unit`                                             |
| E2E test       | `npm run test:e2e`                                              |

Test filtering note: the backend test project uses Microsoft Testing Platform, so targeted runs use test-app options after `--`, for example `dotnet test -- --filter-class Exceptionless.Tests.Controllers.EventControllerTests`.

## Project Structure

```text
src/
├── Exceptionless.AppHost      # Aspire orchestrator (start here)
├── Exceptionless.Core         # Domain logic
├── Exceptionless.Insulation   # Infrastructure (Elasticsearch, Redis, Azure)
├── Exceptionless.Web          # API + Svelte SPA (ClientApp/)
└── Exceptionless.Job          # Background workers
tests/                         # C# tests + HTTP samples
```

## Agents

Available in `.claude/agents/`. Use `@agent-name` to invoke:

- `engineer`: Plans, implements, verifies, reviews, QA tests, commits. Risk-based: micro (typo/config) → standard (bugs/features) → high-risk (auth/billing/data). Delegates to @reviewer and @qa.
- `reviewer`: Adversarial 4-pass analysis (security → machine → correctness → style). Read-only. Supports SILENT_MODE for engineer loops.
- `qa`: QA engineer — dogfood via agent-browser, E2E, API smoke tests. Read-only. Tiered by scope: backend=API smoke, frontend=browser dogfood, fullstack=both.
- `triage`: Issue analyst — 5 Whys for bugs, architecture deep-dives for features/questions, community responses with warmth and depth.
- `pr-reviewer`: PR gate — security pre-screen, dependency audit, delegates to @reviewer, inline GitHub comments, resolves stale comments, verdict. Drafts first, posts after approval.

## Constraints

- Use `npm ci` (not `npm install`)
- Never commit secrets — use environment variables
- NuGet feeds are in `NuGet.Config` — don't add sources
- **Backwards compatibility:** Never break existing public APIs, WebSocket message formats, config keys, or exported library interfaces without explicit user approval. Call out any breaking change as a BLOCKER in reviews.
- **API test files:** Update `tests/http/*.http` files whenever endpoints change (new, modified, or removed).
- **PR descriptions:** When creating a PR, fill out any existing PR template. Provide concise context: what changed, why, new APIs/features/behaviors, and any breaking changes. No essays — just enough for reviewers to understand the value and impact.
- **App URL for QA:** `http://localhost:5200` — probe `/api/v2/about` for health check.
