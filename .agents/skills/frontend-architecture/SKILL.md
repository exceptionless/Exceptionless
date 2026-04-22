---
name: frontend-architecture
description: >
    Use this skill when working on the Svelte SPA's project structure — adding routes, creating
    feature slices, organizing shared components, or understanding the ClientApp directory layout.
    Covers route groups, $lib conventions, barrel exports, API client organization, and vertical
    slice architecture. Apply when deciding where to place new files or components.
---

# Frontend Architecture

Located in `src/Exceptionless.Web/ClientApp`. The Svelte SPA is the primary client.

## Directory Structure

```text
src/
├── lib/
│   ├── features/           # Feature slices (vertical organization)
│   │   ├── auth/
│   │   │   ├── api.svelte.ts
│   │   │   ├── models/
│   │   │   ├── schemas.ts
│   │   │   └── components/
│   │   ├── organizations/
│   │   ├── projects/
│   │   ├── events/
│   │   └── shared/         # Cross-feature shared code
│   ├── components/         # App-wide shared components
│   │   └── ui/             # shadcn-svelte components
│   ├── generated/          # API-generated types
│   └── utils/              # Utility functions
├── routes/
│   ├── (app)/              # Authenticated app routes
│   ├── (auth)/             # Authentication routes
│   └── (public)/           # Public routes
└── app.html
```

## Route Groups

```text
routes/
├── (app)/                  # Requires authentication (app layout with nav)
├── (auth)/                 # Login/signup flows (minimal auth layout)
└── (public)/               # Public pages (marketing layout)
```

## Feature Slices

Organize by feature, aligned with API controllers:

```text
features/organizations/
├── api.svelte.ts           # TanStack Query hooks (see tanstack-query skill)
├── models/
│   └── index.ts            # Re-exports from $lib/generated
├── schemas.ts              # Zod validation schemas
├── options.ts              # Dropdown options, enums
└── components/
    ├── organization-card.svelte
    ├── organization-form.svelte
    └── dialogs/
        └── create-organization-dialog.svelte
```

## Formatter Components (MUST use)

**Always use formatter components instead of custom formatting functions.** Creating a custom formatting function when a component exists is a code review **BLOCKER**.

| Component | Use For |
|-----------|---------|
| `<DateTime>` | Date and time display |
| `<TimeAgo>` | Relative time ("3 hours ago") |
| `<Duration>` | Time durations |
| `<Bytes>` | File sizes, memory |
| `<Number>` | Numeric values with locale formatting |
| `<Boolean>` | True/false display |
| `<Currency>` | Money amounts |
| `<Percentage>` | Percentage values |
| `<DateMath>` | Elasticsearch date math expressions |

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
import { Button } from "$comp/ui/button";       // $lib/components
import { User } from "$features/users/models";  // $lib/features
import { formatDate } from "$shared/formatters"; // $lib/features/shared
```

## Project Svelte Rules

- Prefer `$derived` for computed state and `$effect` for side effects.
- Use `untrack()` inside `$effect` when needed to avoid reactive loops.
- Prefer `kit-query-params` (`queryParamsState`) for route query parameter binding instead of ad-hoc URL parsing.

## Consistency Rule

**Before creating anything new, search the codebase for existing patterns.** Consistency is the most important quality of a codebase:

1. Find the closest existing implementation of what you're building
2. Match its patterns exactly — file structure, naming, imports, component composition
3. Reuse shared utilities and components from `$lib/features/shared/` and `$comp/`
4. If an existing utility almost does what you need, extend it — don't create a parallel one

Pattern divergence is a code review **BLOCKER**, not a nit.

## Navigation Preference

Prefer `href` navigation over `onclick`/`goto`. Use `onclick` only when async logic (e.g. save-then-navigate) is required.

## Composite Component Pattern

Study existing components before creating new ones — dialogs in `/components/dialogs/`, dropdowns via `options.ts` with `DropdownItem<EnumType>[]`, forms via TanStack Form patterns.
