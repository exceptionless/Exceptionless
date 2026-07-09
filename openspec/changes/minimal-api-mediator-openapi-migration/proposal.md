# Proposal: Minimal API + Mediator + OpenAPI Migration

## Summary

Migrate all ASP.NET Core MVC controllers in `src/Exceptionless.Web/Controllers/` to Minimal API endpoints backed by Foundatio.Mediator for command/query dispatch, with runtime and build-time OpenAPI generation.

## Why OpenSpec Is Justified

This change affects:

- **Public API behavior** — Every existing route is being re-implemented in a new hosting model.
- **SDK/client compatibility** — Route paths, response shapes, status codes, headers, and auth must remain identical.
- **Middleware ordering** — ThrottlingMiddleware, OverageMiddleware, and endpoint filters must maintain current behavior.
- **OpenAPI contract** — New generation mechanism replaces the existing Swagger setup.
- **Cross-cutting concerns** — Validation, ProblemDetails, pagination, RFC 6902 JSON Patch, and legacy partial-body compatibility all interact with the new endpoint model.

The scope is large (14 controllers), the compatibility surface is wide, and regression risk without explicit acceptance criteria is high.

## Classification

- **Primary**: Refactor (controller → Minimal API)
- **Secondary**: Infrastructure (Mediator pattern, OpenAPI generation, build-time artifact)

## Affected Areas

| Area | Impact |
|------|--------|
| Backend/API | All public endpoints migrated |
| Tests | New snapshot tests, existing integration tests must pass |
| SDK/client compatibility | Must be zero-breaking-change |
| Docker/deployment | No container changes; build-time OpenAPI artifact added |
| Docs | Scalar docs preserved at /docs/v2/openapi.json |

## Compatibility Risks

| Risk | Mitigation |
|------|-----------|
| Route regression | Route manifest snapshot tests detect any path/method drift |
| Auth bypass | Existing auth policies applied identically; integration tests verify |
| Response shape change | OpenAPI snapshot tests detect schema drift |
| Middleware ordering | Pipeline order preserved; no middleware replaced |
| Validation gap | Automatic validation + MiniValidation covers all current cases |
| Header loss | Endpoint filters replicate current action filters |

## Rollback Plan

1. The migration is incremental (one controller at a time). Each migrated endpoint coexists with the original controller during development.
2. If a regression is detected post-merge, revert the PR that removed the specific controller. The Minimal API endpoint and the controller cannot both be active for the same route, so reverting the controller removal restores prior behavior.
3. OpenAPI snapshot tests and route manifest tests provide immediate CI signal if rollback introduces drift.

## Controllers to Migrate

| Controller | Routes | Priority |
|-----------|--------|----------|
| StatusController | /api/v2/about, queue-stats, notifications/* | Early (simple) |
| UtilityController | /api/v2/search/validate, /api/v2/timezones | Early (simple) |
| TokenController | CRUD for API tokens | Mid |
| SavedViewController | CRUD for saved views | Mid |
| ProjectController | CRUD + config, notifications, integrations | Mid |
| OrganizationController | CRUD + invoices, plans, suspend | Mid |
| StackController | CRUD + mark fixed/critical/snoozed | Mid |
| UserController | CRUD + email verification | Mid |
| WebHookController | CRUD for webhooks | Mid |
| StripeController | Webhook receiver | Mid |
| AuthController | Login, signup, OAuth, forgot-password | Late (complex auth) |
| AdminController | System admin operations | Late |
| EventController | Ingestion, query, count, sessions | Last (highest complexity) |
