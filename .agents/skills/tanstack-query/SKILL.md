---
name: TanStack Query
description: |
  Data fetching and caching with TanStack Query in Svelte. Query patterns, mutations,
  cache invalidation, WebSocket-driven updates, and optimistic updates.
  Keywords: createQuery, createMutation, TanStack Query, query keys, cache invalidation,
  optimistic updates, refetch, stale time, @exceptionless/fetchclient, WebSocket
---

# TanStack Query

> **Documentation:** [tanstack.com/query](https://tanstack.com/query) | Use `context7` for API reference

Centralize API calls in `api.svelte.ts` per feature using TanStack Query with `@exceptionless/fetchclient`.

## Query Basics

```typescript
// src/lib/features/organizations/api.svelte.ts
import { createQuery, createMutation, useQueryClient } from '@tanstack/svelte-query';
import { useFetchClient, type ProblemDetails } from '@exceptionless/fetchclient';

export function getOrganizationsQuery() {
    const client = useFetchClient();

    return createQuery(() => ({
        queryKey: ['organizations'],
        queryFn: async () => {
            const response = await client.getJSON<Organization[]>('/organizations');
            if (!response.ok) {
                throw response.problem;
            }
            return response.data!;
        }
    }));
}
```

## Query Keys Convention

Use a queryKeys factory per feature for type safety and consistency:

```typescript
// From src/lib/features/webhooks/api.svelte.ts
export const queryKeys = {
    type: ['Webhook'] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    project: (id: string | undefined) => [...queryKeys.type, 'project', id] as const,
    deleteWebhook: (ids: string[] | undefined) => [...queryKeys.ids(ids), 'delete'] as const,
    postWebhook: () => [...queryKeys.type, 'post'] as const
};
```

Common patterns:

```typescript
// Resource list
['organizations']
['projects']

// Single resource
['organizations', organizationId]
['projects', projectId]

// Nested resources
['organizations', organizationId, 'projects']
['projects', projectId, 'events']

// Filtered queries
['events', { projectId, status: 'open' }]
```

## Using Queries in Components

```svelte
<script lang="ts">
    import { getOrganizationsQuery } from '$features/organizations/api.svelte';

    const organizationsQuery = getOrganizationsQuery();
</script>

{#if organizationsQuery.isPending}
    <LoadingSpinner />
{:else if organizationsQuery.isError}
    <ErrorMessage error={organizationsQuery.error} />
{:else}
    {#each organizationsQuery.data as org}
        <OrganizationCard {org} />
    {/each}
{/if}
```

## Mutations

```typescript
export function createOrganizationMutation() {
    const client = useFetchClient();
    const queryClient = useQueryClient();

    return createMutation(() => ({
        mutationFn: async (data: CreateOrganizationRequest) => {
            const response = await client.postJSON<Organization>('/organizations', data);
            if (!response.ok) {
                throw response.problem;
            }
            return response.data!;
        },
        onSuccess: () => {
            // Invalidate and refetch organizations list
            queryClient.invalidateQueries({ queryKey: ['organizations'] });
        }
    }));
}
```

## Using Mutations

```svelte
<script lang="ts">
    import { createOrganizationMutation } from '$features/organizations/api.svelte';

    const createMutation = createOrganizationMutation();

    async function handleCreate(data: CreateOrganizationRequest) {
        try {
            const org = await createMutation.mutateAsync(data);
            goto(`/organizations/${org.id}`);
        } catch (error) {
            // Error handled by form or toast
        }
    }
</script>

<Button
    onclick={() => handleCreate(formData)}
    disabled={createMutation.isPending}
>
    {createMutation.isPending ? 'Creating...' : 'Create'}
</Button>
```

## Naming Conventions

Functions follow HTTP verb prefixes:

```typescript
// Queries (GET)
export function getOrganizationsQuery() { ... }
export function getOrganizationQuery(id: string) { ... }
export function getProjectEventsQuery(projectId: string) { ... }

// Mutations
export function postOrganizationMutation() { ... }  // CREATE
export function patchOrganizationMutation() { ... } // UPDATE
export function deleteOrganizationMutation() { ... } // DELETE
```

## Dependent Queries

```typescript
export function getProjectQuery(projectId: string) {
    const client = useFetchClient();

    return createQuery(() => ({
        queryKey: ['projects', projectId],
        queryFn: async () => {
            const response = await client.getJSON<Project>(`/projects/${projectId}`);
            if (!response.ok) throw response.problem;
            return response.data!;
        },
        enabled: !!projectId // Only run when projectId is truthy
    }));
}
```

## Optimistic Updates

```typescript
export function updateOrganizationMutation() {
    const client = useFetchClient();
    const queryClient = useQueryClient();

    return createMutation(() => ({
        mutationFn: async ({ id, data }: { id: string; data: UpdateOrganizationRequest }) => {
            const response = await client.patchJSON<Organization>(`/organizations/${id}`, data);
            if (!response.ok) throw response.problem;
            return response.data!;
        },
        onMutate: async ({ id, data }) => {
            // Cancel in-flight queries
            await queryClient.cancelQueries({ queryKey: ['organizations', id] });

            // Snapshot previous value
            const previous = queryClient.getQueryData<Organization>(['organizations', id]);

            // Optimistically update
            queryClient.setQueryData(['organizations', id], (old: Organization) => ({
                ...old,
                ...data
            }));

            return { previous };
        },
        onError: (err, variables, context) => {
            // Rollback on error
            if (context?.previous) {
                queryClient.setQueryData(['organizations', variables.id], context.previous);
            }
        },
        onSettled: (data, error, { id }) => {
            // Always refetch after mutation
            queryClient.invalidateQueries({ queryKey: ['organizations', id] });
        }
    }));
}
```

## Prefetching

```typescript
export function prefetchOrganization(id: string) {
    const client = useFetchClient();
    const queryClient = useQueryClient();

    return queryClient.prefetchQuery({
        queryKey: ['organizations', id],
        queryFn: async () => {
            const response = await client.getJSON<Organization>(`/organizations/${id}`);
            if (!response.ok) throw response.problem;
            return response.data!;
        }
    });
}
```

## WebSocket-Driven Invalidation

Invalidate queries when WebSocket messages arrive:

```typescript
// From src/lib/features/webhooks/api.svelte.ts
import type { WebSocketMessageValue } from '$features/websockets/models';
import { QueryClient } from '@tanstack/svelte-query';

export async function invalidateWebhookQueries(
    queryClient: QueryClient,
    message: WebSocketMessageValue<'WebhookChanged'>
) {
    const { id, organization_id, project_id } = message;

    if (id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.id(id) });
    }

    if (project_id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.project(project_id) });
    }

    // Fallback: invalidate all if no specific keys
    if (!id && !organization_id && !project_id) {
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
    }
}
```

Wire up in WebSocket handler:

```typescript
// In WebSocket message handler
onMessage('WebhookChanged', (message) => {
    invalidateWebhookQueries(queryClient, message);
});
```
