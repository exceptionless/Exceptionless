# Spec: Local Development and Testing

Baseline spec capturing how the Exceptionless platform is started, developed, and tested locally.

## Entry Point

- `Exceptionless.AppHost` (Aspire orchestrator) is the single local startup command.
- CLI: `aspire run`; IDE: run `Exceptionless.AppHost` as startup project.
- The AppHost starts all required infrastructure (Elasticsearch, Redis, Azure Storage emulator, Mailpit) and the application services (API, Jobs).

## Infrastructure Services

| Service | Default Port | Notes |
|---------|-------------|-------|
| Elasticsearch | 9200 | Persistent volume `exceptionless.data.v1` |
| Redis | 6379 | Persistent container |
| Azure Storage Emulator | — | Blob + Queue endpoints |
| Mailpit (SMTP) | 1025 (SMTP), 8025 (UI) | Captures outbound mail |

## Application Services

| Service | Endpoint | Notes |
|---------|----------|-------|
| API (`Exceptionless.Web`) | https://localhost:7121, http://localhost:7110 | Health: `/health`, Ready: `/ready` |
| Jobs (`Exceptionless.Job`) | — | Runs all background jobs by default; supports named single-job mode |

## Developer Seed Data

- In `Development` mode a global admin user is auto-created: `admin@exceptionless.test` / `tester`.
- Projects can generate sample data via `POST /api/v2/projects/{id}/sample-data`.

## Backend Tests

- Command: `dotnet test`
- Filtered: `dotnet test -- --filter-class <Fully.Qualified.TestClass>`
- Tests bootstrap their own infrastructure; no external services need to be running.
- Integration tests derive from `IntegrationTestsBase` (which provisions Elasticsearch, Redis, etc.).

## Frontend Development

| App | Path | Build |
|-----|------|-------|
| Svelte 5 (WIP) | `src/Exceptionless.Web/ClientApp` | `npm ci && npm run build` |
| Angular (production) | `src/Exceptionless.Web/ClientApp.angular` | `npm ci && npm run build` |

- Svelte unit tests: `npm run test:unit`
- Svelte E2E tests: `npm run test:e2e`
- Svelte check/lint: `npm run check`, `npm run lint`

## Contracts & Expectations

- `npm ci` is used (never `npm install`).
- Secrets are never committed; use env vars.
- NuGet feeds live in `NuGet.Config`; do not add package sources.
- `tests/http/*.http` files must be updated when endpoints change.

**Elasticsearch version:** Minimum is latest 8.x; the upper bound is limited by NEST client compatibility. No specific pinned version is a formal minimum.

**Resource requirements:** No formal local development resource requirements are documented. Users need Docker and whatever resources the configured services require. Kubernetes manifests provide production/dev examples but are not local-dev minimums.
