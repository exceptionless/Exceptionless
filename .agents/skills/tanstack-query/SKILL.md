---
name: tanstack-query
description: Manage Svelte API queries, mutations, cache updates, and WebSocket invalidation.
---

# TanStack Query

Centralize API calls in each feature's `api.svelte.ts`, using `@tanstack/svelte-query` and `@exceptionless/fetchclient`.

Use the current organization and webhook implementations under `src/Exceptionless.Web/ClientApp/src/lib/features/` as examples. Preserve their response/error types and signal handling.

- Share a feature's `queryKeys` factory across queries, mutations, and invalidation.
- Name operations `get{Resource}Query`, `post{Resource}Mutation`, `patch{Resource}Mutation`, and `delete{Resource}Mutation`.
- Gate queries on required authentication or identifiers.
- Pass the query cancellation signal through the fetch client.
- For optimistic updates, cancel competing queries, snapshot the cache, restore on failure, and reconcile with server state after completion.
- Wire WebSocket changes through the feature's invalidation helper. Match invalidation scope to the message identifiers.

Verify failure recovery and relevant cache interactions, not just the successful response.

## Mutation and cache lifecycle

Keep reactive query options in the factory passed to `createQuery` or `createMutation`, following the current feature implementation. Include identifiers and relevant query parameters in its key factory so distinct results do not share a cache entry.

For an optimistic mutation, `onMutate` cancels affected in-flight queries and saves the previous value before updating the cache. `onError` uses that context to roll back; completion reconciles affected queries with server state, commonly through `onSettled`. Preserve unrelated cache entries and consider overlapping mutations before restoring an older snapshot.

Await invalidation or refetching when the calling UI depends on it before closing or navigating. Match the existing feature's `FetchClientResponse` and ProblemDetails handling instead of assuming every request throws or every mutation returns an unwrapped model.

WebSocket invalidation must use the same key factories as queries. Inspect both the feature helper and its caller when changing message handling; verify identifier-specific and collection updates.
