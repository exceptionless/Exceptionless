# Spec: Frontend UI

Baseline spec for the Exceptionless frontend applications.

> **Implementation-derived values** are included to guide future changes, but they are not public compatibility contracts unless explicitly stated in the relevant requirement.

## Two Frontends

| App | Path | Status | Stack |
|-----|------|--------|-------|
| Angular (legacy) | `src/Exceptionless.Web/ClientApp.angular` | Production — powers the main site | AngularJS, Grunt, Less |
| Svelte 5 | `src/Exceptionless.Web/ClientApp` | Under development | Svelte 5, SvelteKit, Tailwind, shadcn-svelte |

## Angular App (Production)

Key directories: `app/`, `components/`, `less/`, `img/`, `lang/`, `grunt/`.

- Build: `npm ci && npm run build`
- Served as static files by `Exceptionless.Web`.

## Svelte App (Development)

### Routes (SvelteKit file-based routing)

| Group | Routes |
|-------|--------|
| `(auth)` | `/login`, `/signup`, `/logout`, `/forgot-password`, `/reset-password/[token]` |
| `(app)` | `/events`, `/events/[eventId]`, `/events/[slug]`, `/issues`, `/issues/[stackId]`, `/issues/[slug]` |
| `(app)` | `/account/*` (general, notifications, user-management), `/organization/*` |
| status | `/status` |

### Design System

- Canonical reference: `DESIGN.md`
- Runtime tokens: `src/Exceptionless.Web/ClientApp/src/app.css`
- Component library: shadcn-svelte (Tailwind-based)
- Fonts: Inter Variable (sans), Source Code Pro Variable (mono)
- Light/dark mode via CSS custom properties

### Data Fetching

- TanStack Query for server state.
- WebSocket (`/api/v2/push`) for real-time cache invalidation.
- Saved views use optimistic writes with WebSocket-driven invalidation delays.

### Build & Test

- `npm ci && npm run build` — production build
- `npm run dev` — dev server
- `npm run check` — svelte-check
- `npm run lint` — ESLint
- `npm run test:unit` — Vitest
- `npm run test:e2e` — Playwright

## Requirements

### Requirement: Angular and Svelte ownership boundaries are explicit

The legacy Angular UI remains the main site UI while the Svelte 5 UI is under development unless a future change explicitly changes ownership, routing, or migration status.

#### Scenario: UI behavior is changed

Given a UI behavior change is proposed
When the affected surface is identified
Then the change must state whether it applies to Angular, Svelte, or both.

### Requirement: Angular and Svelte are served through separate runtime entry points

The current application routing serves the legacy Angular app for normal non-API/non-docs traffic and serves the Svelte app under the `/next` route.

#### Scenario: UI routing changes

Given a change modifies frontend routing, fallback routes, or app entry points
When the change is proposed
Then the change must document whether Angular, Svelte, or both are affected.

## Compatibility Boundaries

- The Angular app is the production UI; changes must not break existing functionality.
- API contracts consumed by the Angular app (route shapes, response formats) cannot change without updating the Angular client.
- The Svelte app consumes the same `/api/v2` endpoints; no separate API.
- WebSocket push message types drive UI updates in both apps.
- Design tokens in `app.css` are the source of truth for the Svelte UI.

### Requirement: Angular app uses WebSocket push for real-time updates

The Angular app connects to the `/api/v2/push` WebSocket endpoint using a resilient auto-reconnecting WebSocket client (`components/websocket/websocket-service.js`). It does not use polling.

#### Scenario: WebSocket push behavior changes

Given a change modifies the WebSocket push endpoint, message format, or connection behavior
When the change is proposed
Then the change must account for the Angular app's WebSocket client behavior.

**Note on migration timeline:** The Angular → Svelte migration is active ongoing work with no fixed timeline. Both apps are maintained concurrently until migration is complete.
