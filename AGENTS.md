# Exceptionless

Real-time error monitoring platform handling billions of requests (ASP.NET Core 10 + Svelte 5). Act as a distinguished engineer focusing on readability, performance while maintaining backwards compatibility.

## Start Here

- Start everything with the Aspire CLI: `aspire run`, or run `Exceptionless.AppHost` directly from your IDE.
- The AppHost launches required services (Elasticsearch, Redis, API, Job worker) and opens the Aspire dashboard. Integration tests bootstrap their own infrastructure.

## Common Commands

| Task                | Command                                                         |
| ------------------- | --------------------------------------------------------------- |
| Run (Aspire)        | `aspire run`                                                    |
| Backend build       | `dotnet build`                                                  |
| Backend test        | `dotnet test`                                                   |
| Frontend build      | `cd src/Exceptionless.Web/ClientApp && npm ci && npm run build` |
| Frontend unit tests | `cd src/Exceptionless.Web/ClientApp && npm run test:unit`       |
| Frontend E2E tests  | `cd src/Exceptionless.Web/ClientApp && npm run test:e2e`        |

## Repo-Specific Notes

- Backend test filtering uses Microsoft Testing Platform test-app options after `--`, for example `dotnet test -- --filter-class Exceptionless.Tests.Controllers.EventControllerTests`.
- Elasticsearch-backed repository or job tests should derive from `IntegrationTestsBase`, not `TestWithServices`.
- The current main site UI is the legacy Angular app in `src/Exceptionless.Web/ClientApp.angular`; the folders you will usually touch there are `app/`, `components/`, `less/`, `img/`, `lang/`, and `grunt/`.
- The Svelte 5 UI in `src/Exceptionless.Web/ClientApp` is still under development.
- Standard pull requests build `api`, `job`, and `app` images. The all-in-one `exceptionless` image is only built for tags.
- If you touch Docker publish stages that use `dotnet publish --no-build`, make sure the stage still has the build output and NuGet package cache available.

## Project Structure

```text
src/
├── Exceptionless.AppHost      # Aspire orchestrator (start here)
├── Exceptionless.Core         # Domain logic
├── Exceptionless.Insulation   # Infrastructure (Elasticsearch, Redis, Azure)
├── Exceptionless.Web          # API host
│   ├── ClientApp.angular/     # Legacy Angular UI that still powers the main site
│   └── ClientApp/             # Svelte 5 UI that is still under development
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
- Prefer additive documentation updates — don't replace strategic docs wholesale, extend them
- **Backwards compatibility:** Never break existing public APIs, WebSocket message formats, config keys, or exported library interfaces without explicit user approval. Call out any breaking change as a BLOCKER in reviews.
- **API test files:** Update `tests/http/*.http` files whenever endpoints change (new, modified, or removed).
- **PR descriptions:** When creating a PR, fill out any existing PR template. Provide concise context: what changed, why, new APIs/features/behaviors, and any breaking changes. No essays — just enough for reviewers to understand the value and impact.
- **App URL for QA:** `http://localhost:7110` — probe `/api/v2/about` for health check.
- **Never test against production:** Always dogfood, QA test, and run API smoke tests against `localhost` only. Never use production URLs (e.g., `be.exceptionless.io`) in scripts, tests, or browser automation. Start the app locally via `aspire run` or the AppHost before testing.
- **Fix what you find:** If you encounter a broken test, bug, or issue during your work — fix it. Never label something "pre-existing" and move on. Own every problem you touch.

## Frontend Notes

- Saved-view optimistic writes must update both `queryKeys.view(organizationId, view)` and `queryKeys.organization(organizationId)` caches immediately. `invalidateSavedViewQueries` delays `SavedViewChanged` `Added` and `Saved` WebSocket invalidations for Elasticsearch refresh safety, and the picker still uses local 1.5s invalidation timers for rename/default/delete flows.
