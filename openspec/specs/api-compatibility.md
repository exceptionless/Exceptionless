# Spec: API Compatibility

Baseline spec for the Exceptionless REST API contracts. All endpoints are versioned under `/api/v2`.

> **Implementation-derived values** are included to guide future changes, but they are not public compatibility contracts unless explicitly stated in the relevant requirement.

## API Prefix

All controllers use `API_PREFIX` which resolves to `/api/v2`.

## Controllers & Route Roots

| Controller | Route | Purpose |
|-----------|-------|---------|
| AuthController | `/api/v2/auth` | Login, signup, OAuth, password reset |
| OrganizationController | `/api/v2/organizations` | CRUD, plans, invoices, users, suspend, data, features |
| ProjectController | `/api/v2/projects` | CRUD, config, tokens, sample data, notifications |
| EventController | `/api/v2/events` | Query, submit, count, sessions, user-description |
| StackController | `/api/v2/stacks` | Query, status changes, mark-fixed, snooze, promote, links |
| UserController | `/api/v2/users` | User profile, current user |
| TokenController | `/api/v2/tokens` | API key management |
| WebHookController | `/api/v2/webhooks` | Webhook CRUD |
| SavedViewController | `/api/v2/saved-views` | Saved view CRUD |
| StatusController | `/api/v2` | About, queue-stats, notifications, health |
| AdminController | `/api/v2/admin` | System admin operations |
| StripeController | `/api/v2/stripe` | Billing webhook receiver |
| UtilityController | `/api/v2` | Utility endpoints |

## Authentication

- Bearer token (JWT) via `Authorization: Bearer {token}` header.
- Client API key via `access_token` query parameter (event submission).
- Login: `POST /api/v2/auth/login` with `{ email, password }` → returns `{ token }`.
- Signup: `POST /api/v2/auth/signup` with `{ email, password, name }`.
- OAuth: `POST /api/v2/auth/{provider}` (e.g., `google`).

## WebSocket Push

- Endpoint: `/api/v2/push`
- Requires authenticated WebSocket upgrade.
- Server pushes messages via `IMessageBus` → `WebSocketConnectionManager`.
- Incoming messages from client are ignored (read-only push channel).

## Health & Readiness

- `/health` — ASP.NET health check.
- `/ready` — readiness probe.
- `GET /api/v2/about` — authenticated system info.

## Compatibility Boundaries

- Public API routes, request/response shapes, query parameters, and status codes must not change without explicit approval.
- WebSocket message format (JSON text frames) and the `/api/v2/push` path are stable contracts.
- Client tokens (`access_token`) must continue to work for event submission.
- Bulk operations accept comma-separated IDs in the route (e.g., `POST /api/v2/stacks/{id1},{id2}/change-status`).

## Requirements

### Requirement: Generated API documentation remains available

Exceptionless must provide generated API documentation for API consumers. The current implementation uses Scalar (replacing the former Swashbuckle/Swagger tooling).

#### Scenario: API documentation tooling changes

Given the API documentation tooling changes
When the API is built or hosted
Then generated API documentation must remain available for API consumers.

## Internal Implementation Baselines

**Throttling (implementation-derived):** `AppOptions.ApiThrottleLimit` defaults to unlimited in development and 3500 in production. Plans enforce monthly and hourly event limits. Clients are expected to discard submitted events briefly when throttled or over limit; duplicate bursts are rolled up.

### Requirement: API throttling headers are implementation-visible but not documented public contract

Exceptionless may expose rate-limit response headers (e.g., `RateLimit`, `RateLimitRemaining`) when API throttling is enabled. These headers are implementation behavior and must not be changed casually, but they are not treated as a documented public API contract unless API documentation defines their exact names and semantics.

#### Scenario: API throttling behavior changes

Given a change modifies API throttling limits, middleware, or response headers
When the change is proposed
Then the change must document whether `RateLimit` or `RateLimitRemaining` behavior changes and whether API documentation or client behavior must be updated.
