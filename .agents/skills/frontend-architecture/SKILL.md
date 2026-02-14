---
name: Frontend Architecture
description: |
  Svelte SPA architecture for Exceptionless. Route groups, lib structure, API client,
  feature slices, and barrel exports.
  Keywords: route groups, $lib, feature slices, api-client, barrel exports, index.ts,
  vertical slices, shared components, generated models, ClientApp structure
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
import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
import { useFetchClient } from '@exceptionless/fetchclient';
import type { Organization, CreateOrganizationRequest } from './models';

export function getOrganizationsQuery() {
    const client = useFetchClient();

    return createQuery(() => ({
        queryKey: ['organizations'],
        queryFn: async () => {
            const response = await client.getJSON<Organization[]>('/organizations');
            if (!response.ok) throw response.problem;
            return response.data!;
        }
    }));
}

export function postOrganizationMutation() {
    const client = useFetchClient();
    const queryClient = useQueryClient();

    return createMutation(() => ({
        mutationFn: async (data: CreateOrganizationRequest) => {
            const response = await client.postJSON<Organization>('/organizations', data);
            if (!response.ok) throw response.problem;
            return response.data!;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['organizations'] });
        }
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
    UpdateOrganizationRequest
} from '$lib/generated';

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
export { getOrganizationsQuery, postOrganizationMutation } from './api.svelte';
export type { Organization, CreateOrganizationRequest } from './models';
export { organizationSchema } from './schemas';
```

## Shared Components

Place truly shared components in appropriate locations:

```text
lib/
├── features/shared/        # Shared between features
│   ├── components/
│   │   ├── formatters/     # Boolean, date, number formatters
│   │   ├── loading/
│   │   └── error/
│   └── utils/
└── components/             # App-wide components
    ├── ui/                 # shadcn-svelte
    ├── layout/
    └── dialogs/            # Global dialogs
```

## Generated Types

When API contracts change:

```bash
npm run generate-models
```

Prefer regeneration over hand-writing DTOs. Generated types live in `$lib/generated`.

## Import Aliases

```typescript
// Configured in svelte.config.js
import { Button } from '$comp/ui/button';        // $lib/components
import { User } from '$features/users/models';   // $lib/features
import { formatDate } from '$shared/formatters'; // $lib/features/shared
```

## Composite Component Pattern

Study existing components before creating new ones:

- **Dialogs**: See `/components/dialogs/`
- **Dropdowns**: Use `options.ts` with `DropdownItem<EnumType>[]`
- **Forms**: Follow TanStack Form patterns in `svelte-forms` skill
