---
name: frontend-architecture
description: >
    Use this skill as the frontend entrypoint for the Svelte 5 app in ClientApp — adding routes,
    creating feature slices, writing Svelte components, applying TypeScript conventions, using
    shared UI/formatter components, handling accessibility, or adding frontend tests. The legacy
    Angular app still powers most of the current site in ClientApp.angular, but all new frontend
    development should be Svelte UI only unless the user explicitly asks for Angular or legacy UI
    changes.
---

# Frontend Architecture

Exceptionless.Web currently has two frontend codebases:

- `src/Exceptionless.Web/ClientApp.angular` is the legacy Angular UI and still powers most of the current site. Treat it as maintenance-only. Do not edit it, add routes to it, or port patterns from it unless the user explicitly asks for Angular or legacy UI changes. The main folders there are `app/`, `components/`, `less/`, `img/`, `lang/`, and `grunt/`.
- `src/Exceptionless.Web/ClientApp` is the Svelte 5 app and the target for all new frontend UI development.

Default to Svelte. If the request says "frontend", "UI", "page", "component", "form", or "route" and does not explicitly mention Angular, work in `src/Exceptionless.Web/ClientApp`.

Only work in `ClientApp.angular` when the user specifically asks for Angular, legacy UI, or a bug fix in a screen that only exists there.

## Directory Structure

```text
src/
├── lib/
│   ├── features/           # Feature slices (vertical organization)
│   │   ├── auth/
│   │   │   ├── api.svelte.ts
│   │   │   ├── models.ts
│   │   │   ├── schemas.ts
│   │   │   └── validators.ts
│   │   ├── organizations/
│   │   ├── projects/
│   │   ├── events/
│   │   └── shared/         # Cross-feature shared code
│   ├── generated/          # API-generated types
│   └── utils/              # Utility functions
├── routes/
│   ├── (app)/              # Authenticated app routes
│   ├── (auth)/             # Authentication routes
│   ├── status/             # Public status route
│   ├── +layout.svelte
│   └── routes.svelte.ts
└── app.html
```

## Feature Slices

Organize by feature and match the nearest existing feature shape before adding files:

```text
features/organizations/
├── api.svelte.ts           # TanStack Query hooks (see tanstack-query skill)
├── models.ts               # Feature-local types and generated type aliases
├── schemas.ts              # Zod validation schemas
├── options.ts              # Dropdown options, enums
└── components/
    └── *.svelte
```

## Formatter Components (MUST use)

Use formatter components instead of custom formatting functions when a component exists.

| Component | Path | Use For |
|-----------|------|---------|
| `<DateTime>` | `$comp/formatters/date-time.svelte` | Date and time display |
| `<TimeAgo>` | `$comp/formatters/time-ago.svelte` | Relative time ("3 hours ago") |
| `<Duration>` | `$comp/formatters/duration.svelte` | Time durations |
| `<Bytes>` | `$comp/formatters/bytes.svelte` | File sizes, memory |
| `<Number>` | `$comp/formatters/number.svelte` | Numeric values with locale formatting |
| `<NumberCompact>` | `$comp/formatters/number-compact.svelte` | Compact numeric values |
| `<Boolean>` | `$comp/formatters/boolean.svelte` | True/false display |
| `<Currency>` | `$comp/formatters/currency.svelte` | Money amounts |
| `<Percentage>` | `$comp/formatters/percentage.svelte` | Percentage values |
| `<DateMath>` | `$comp/formatters/date-math.svelte` | Elasticsearch date math expressions |

```svelte
<!-- CORRECT -->
<DateTime value={event.date} />
<Bytes value={event.size} />

<!-- WRONG: never do this -->
{formatDateTime(event.date)}
{new Date(event.date).toLocaleString()}
```

## Import Aliases

```typescript
import { Button } from "$comp/ui/button";       // shared components
import { User } from "$features/users/models";  // $lib/features
import { cn } from "$lib/utils";                // $lib
```

## Project Svelte Rules

- Prefer `$derived` for computed state and `$effect` for side effects.
- Use `untrack()` inside `$effect` when needed to avoid reactive loops.
- Prefer `kit-query-params` (`queryParamsState`) for route query parameter binding instead of ad-hoc URL parsing.
- Use Svelte 5 event attributes such as `onclick` and `oninput`; do not use legacy `on:click` syntax in new code.
- Use snippets (`{#snippet children(...)}` / `{@render children?.()}`) instead of slots in new components.
- Use array class syntax or the local `cn()` helper for conditional classes.

## Consistency Rule

Before creating anything new, search the Svelte app for existing patterns:

1. Find the closest Svelte implementation of what you're building.
2. Match its file structure, naming, imports, and component composition.
3. Reuse shared utilities and components from `$shared/` and `$comp/`.
4. If an existing utility almost does what you need, extend it instead of creating a parallel one.

Do not copy architecture from the legacy Angular app into Svelte.

## TypeScript Rules

- Use kebab-case for files and directories.
- Always use braces for control flow statements.
- Use block-bodied arrow functions when the function body has statements.
- Do not abbreviate identifiers: use `organization`, not `org`; `filter`, not `filt`.
- Prefer named imports. Namespace imports are acceptable for shadcn composite components such as `Dialog`, `DropdownMenu`, `Field`, and `Card`.
- Avoid `any`; use generated types, explicit interfaces, `unknown`, or type guards.
- Always await promises or intentionally handle fire-and-forget work.

## UI Components

- Prefer existing shadcn-svelte components from `$comp/ui/*` for buttons, inputs, menus, dialogs, cards, badges, separators, empty states, skeletons, and form fields.
- Native semantic elements are still appropriate for document structure (`main`, `section`, `nav`, `form`, `table`, `button` when no app component is needed). Do not replace semantics with generic styled `div`s.
- For detailed shadcn composition rules, use the `shadcn-svelte` skill.
- Match Exceptionless' operational product UI: dense, restrained, scannable, and consistent with nearby screens. Avoid marketing-style hero layouts and decorative visual experiments inside the app.

## Accessibility

- Use semantic landmarks and a single clear heading hierarchy.
- Every form control needs an associated label or accessible name.
- Icon-only buttons need an `aria-label`; decorative icons should be `aria-hidden="true"`.
- Dialogs must have a title, even if visually hidden.
- Error states should set `aria-invalid` and connect help/error text with `aria-describedby`.
- Preserve keyboard navigation and visible focus states.

## Frontend Tests

- Unit/component tests use Vitest and Testing Library, colocated as `.test.ts` or `.spec.ts`.
- Prefer accessible queries: `getByRole`, `getByLabelText`, then visible text; use test IDs only when semantics are not enough.
- E2E tests use Playwright and must target local app URLs only. Prefer role/label selectors over CSS selectors.
- Add focused tests for changed behavior; do not broaden coverage unrelated to the task.
- During iteration, run the smallest relevant test or targeted verification. Do not run broad `npm run check`, `npm run lint`, or `npm run format` after every small edit; save those for pre-push/pre-PR verification or explicit user requests.

## Navigation Preference

Prefer `href` navigation over `onclick`/`goto`. Use `onclick` only when async logic (e.g. save-then-navigate) is required.

## Composite Component Pattern

Study nearby feature components before creating new ones. Use `options.ts` for shared option sets, shadcn composite imports for dialogs/menus/cards/fields, and the `tanstack-form` skill for forms.
