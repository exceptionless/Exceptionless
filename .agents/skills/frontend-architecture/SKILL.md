---
name: frontend-architecture
description: >
    Use this skill when working on the Svelte 5 app in ClientApp — adding routes, creating
    feature slices, organizing shared components, or understanding the ClientApp directory layout.
    Covers route groups, $lib conventions, barrel exports, API client organization, and vertical
    slice architecture. Apply when deciding where to place new files or components. The legacy
    Angular app that still powers most of the site lives beside it in ClientApp.angular.
---

# Frontend Architecture

Exceptionless.Web currently has two frontend codebases:

- `src/Exceptionless.Web/ClientApp.angular` is the legacy Angular UI and still powers most of the site. The main folders there are `app/`, `components/`, `less/`, `img/`, `lang/`, and `grunt/`.
- `src/Exceptionless.Web/ClientApp` is the Svelte 5 app that is still under development.

Use this skill for `ClientApp` work.

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

Organize routes by authentication/layout requirements:

```text
routes/
├── (app)/                  # Requires authentication
│   ├── +layout.svelte      # App layout with nav
│   ├── organizations/
│   └── projects/
├── (auth)/                 # Login/signup flows
│   ├── +layout.svelte      # Minimal auth layout
│   ├── login/
│   └── signup/
└── (public)/               # Public pages
    ├── +layout.svelte      # Marketing layout
    └── pricing/
```

## Feature Slices

Organize by feature, aligned with API controllers:

```text
features/organizations/
├── api.svelte.ts           # TanStack Query hooks
├── models/
│   └── index.ts            # Re-exports from generated
├── schemas.ts              # Zod validation schemas
├── options.ts              # Dropdown options, enums
└── components/
    ├── organization-card.svelte
    ├── organization-form.svelte
    └── dialogs/
        └── create-organization-dialog.svelte
```

## API Client Pattern

Centralize API calls per feature:

```typescript
// features/organizations/api.svelte.ts
import {
    createQuery,
    createMutation,
    useQueryClient,
} from "@tanstack/svelte-query";
import { useFetchClient } from "@exceptionless/fetchclient";
import type { Organization, CreateOrganizationRequest } from "./models";

export function getOrganizationsQuery() {
    const client = useFetchClient();

    return createQuery(() => ({
        queryKey: ["organizations"],
        queryFn: async () => {
            const response =
                await client.getJSON<Organization[]>("/organizations");
            if (!response.ok) throw response.problem;
            return response.data!;
        },
    }));
}

export function postOrganizationMutation() {
    const client = useFetchClient();
    const queryClient = useQueryClient();

    return createMutation(() => ({
        mutationFn: async (data: CreateOrganizationRequest) => {
            const response = await client.postJSON<Organization>(
                "/organizations",
                data,
            );
            if (!response.ok) throw response.problem;
            return response.data!;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["organizations"] });
        },
    }));
}
```

## Model Re-exports

Re-export generated models through feature model folders:

```typescript
// features/organizations/models/index.ts
export type {
    Organization,
    CreateOrganizationRequest,
    UpdateOrganizationRequest,
} from "$lib/generated";

// Add feature-specific types
export interface OrganizationWithStats extends Organization {
    eventCount: number;
    projectCount: number;
}
```

## Barrel Exports

Use `index.ts` for clean imports:

```typescript
// features/organizations/index.ts
export { getOrganizationsQuery, postOrganizationMutation } from "./api.svelte";
export type { Organization, CreateOrganizationRequest } from "./models";
export { organizationSchema } from "./schemas";
```

## Shared Components

Place truly shared components in appropriate locations:

```text
lib/
├── features/shared/        # Shared between features
│   ├── components/
│   │   ├── formatters/     # Boolean, date, number, bytes, duration, currency, percentage, time-ago formatters
│   │   ├── loading/
│   │   └── error/
│   └── utils/
└── components/             # App-wide components
    ├── ui/                 # shadcn-svelte
    ├── layout/
    └── dialogs/            # Global dialogs
```

### Formatter Components (MUST use — never write custom formatting functions)

The `formatters/` directory contains Svelte components for displaying formatted values. **Always use these instead of writing custom formatting functions like `formatDateTime()` or `formatBytes()`.**

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
<!-- CORRECT: use the formatter component -->
<DateTime value={event.date} />
<TimeAgo value={event.date} />
<Bytes value={event.size} />

<!-- WRONG: never do this -->
{formatDateTime(event.date)}
{new Date(event.date).toLocaleString()}
```

**Consistency rule**: If a formatter component exists for a data type, you MUST use it. Creating a custom formatting function when a component already exists is a code review BLOCKER.

## Generated Types

When API contracts change:

```bash
npm run generate-models
```

Prefer regeneration over hand-writing DTOs. Generated types live in `$lib/generated`.

## Import Aliases

```typescript
// Configured in svelte.config.js
import { Button } from "$comp/ui/button"; // $lib/components
import { User } from "$features/users/models"; // $lib/features
import { formatDate } from "$shared/formatters"; // $lib/features/shared
```

## Consistency Rule

**Before creating anything new, search the codebase for existing patterns.** Consistency is the most important quality of a codebase:

1. Find the closest existing implementation of what you're building
2. Match its patterns exactly — file structure, naming, imports, component composition
3. Reuse shared utilities and components from `$lib/features/shared/` and `$comp/`
4. If an existing utility almost does what you need, extend it — don't create a parallel one

Pattern divergence is a code review **BLOCKER**, not a nit.

## Composite Component Pattern

Study existing components before creating new ones:

- **Dialogs**: See `/components/dialogs/`
- **Dropdowns**: Use `options.ts` with `DropdownItem<EnumType>[]`
- **Forms**: Follow TanStack Form patterns in `svelte-forms` skill
