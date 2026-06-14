---
name: tanstack-query
description: >
    Use this skill when fetching data, managing server state, or handling API mutations in
    the Svelte frontend. Covers createQuery, createMutation, query keys, cache invalidation,
    optimistic updates, and WebSocket-driven refetching. Apply when adding API calls, managing
    loading/error states, or coordinating cache updates after mutations.
---

# TanStack Query

> **Documentation:** [tanstack.com/query](https://tanstack.com/query). Use official docs when the local pattern is not enough.

Centralize API calls in `api.svelte.ts` per feature using TanStack Query with `@exceptionless/fetchclient`.

## Query Basics

```typescript
// src/lib/features/organizations/api.svelte.ts
import { createQuery, createMutation, useQueryClient } from "@tanstack/svelte-query";
import { type FetchClientResponse, type ProblemDetails, useFetchClient } from "@exceptionless/fetchclient";
import { accessToken } from "$features/auth/index.svelte";

const queryKeys = {
    type: ["Organization"] as const,
};

export function getOrganizationsQuery() {
    return createQuery<FetchClientResponse<Organization[]>, ProblemDetails>(() => ({
        enabled: () => !!accessToken.current,
        queryKey: queryKeys.type,
        queryFn: async ({ signal }: { signal: AbortSignal }) => {
            const client = useFetchClient();
            const response = await client.getJSON<Organization[]>("/organizations", { signal });
            return response;
        },
    }));
}
```

## Query Keys Convention

Use a `queryKeys` factory per feature for type safety and consistency:

```typescript
export const queryKeys = {
    type: ["Webhook"] as const,
    id: (id: string | undefined) => [...queryKeys.type, id] as const,
    ids: (ids: string[] | undefined) => [...queryKeys.type, ...(ids ?? [])] as const,
    project: (id: string | undefined) => [...queryKeys.type, "project", id] as const,
    deleteWebhook: (ids: string[] | undefined) => [...queryKeys.ids(ids), "delete"] as const,
    postWebhook: () => [...queryKeys.type, "post"] as const,
};
```

Prefer the feature's `queryKeys` factory over ad-hoc arrays so WebSocket invalidation and cache updates share the same keys.

## Mutations

```typescript
export function postOrganizationMutation() {
    const queryClient = useQueryClient();

    return createMutation(() => ({
        mutationFn: async (data: CreateOrganizationRequest) => {
            const client = useFetchClient();
            const response = await client.postJSON<Organization>("/organizations", data);
            return response.data!;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: queryKeys.type });
        },
    }));
}
```

## Naming Conventions

| Pattern | Naming | Example |
|---------|--------|---------|
| Query (GET) | `get{Resource}Query` | `getOrganizationsQuery()` |
| Create (POST) | `post{Resource}Mutation` | `postOrganizationMutation()` |
| Update (PATCH) | `patch{Resource}Mutation` | `patchOrganizationMutation()` |
| Delete (DELETE) | `delete{Resource}Mutation` | `deleteOrganizationMutation()` |

## Dependent Queries

Use `enabled` to conditionally run queries: `enabled: !!projectId`.

## Optimistic Updates

For mutations that update cached data optimistically: use `onMutate` to cancel in-flight queries, snapshot previous value via `getQueryData`, and apply optimistic update via `setQueryData`. Use `onError` to rollback from snapshot, and `onSettled` to always `invalidateQueries` for the final refetch.

## WebSocket-Driven Invalidation

Invalidate queries when WebSocket messages arrive:

```typescript
export async function invalidateWebhookQueries(
    queryClient: QueryClient,
    message: WebSocketMessageValue<"WebhookChanged">,
) {
    const { id, organization_id, project_id } = message;

    if (id) await queryClient.invalidateQueries({ queryKey: queryKeys.id(id) });
    if (project_id) await queryClient.invalidateQueries({ queryKey: queryKeys.project(project_id) });
    if (!id && !organization_id && !project_id)
        await queryClient.invalidateQueries({ queryKey: queryKeys.type });
}
```

Wire WebSocket messages from the app layout to the feature invalidation helper.
