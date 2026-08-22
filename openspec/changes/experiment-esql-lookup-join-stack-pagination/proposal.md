# Proposal: Experiment with ES|QL Lookup-Join Stack Pagination

## Summary

Replace the growing terms-aggregation/skip path used by event stack-rollup queries with an experimental Elasticsearch 9.5 ES|QL pipeline that:

1. reads the existing daily event indices for the requested time range,
2. uses `LOOKUP JOIN` to attach and filter the canonical stack document from the versioned stack index,
3. aggregates the filtered event rows by `stack_id`,
4. applies a deterministic mode-specific sort with `stack_id` as the final tie-breaker, and
5. pages with opaque `before` and `after` keyset cursors.

This specifically targets the `stack_recent`, `stack_frequent`, `stack_new`, and `stack_users` modes on event list endpoints. Those modes power the organization-level stacks page. It also establishes cursor behavior that direct stack endpoints can reuse, while leaving direct stack-only queries on `IStackRepository` unless an event join is actually needed.

## User-visible behavior

- The four existing stack-rollup modes return the same stack summary shape, filters, metrics, mode ordering, plan enforcement, and authorization boundaries.
- Requests without `page` use cursor paging. Responses include opaque `before` and `after` links when applicable.
- `after` moves forward in the canonical result order; `before` moves backward and returns items in the same canonical order.
- Existing `page` + `limit` requests remain supported by the legacy implementation during the experiment.
- A request cannot combine `page` with `before` or `after`, or combine `before` with `after`.
- Explicit `sort` remains invalid for the four stack-rollup modes. Their fixed sorts remain:
  - `stack_recent`: last occurrence descending
  - `stack_frequent`: summed occurrence count descending
  - `stack_new`: first occurrence descending, restricted by the existing stack-first-occurrence time behavior
  - `stack_users`: distinct users descending
- Cursor tokens are opaque and query-bound. Malformed cursors or cursors reused with a different scope, filter, time range, mode, or sort contract return `400 Bad Request`.
- Cursor paging provides a total deterministic order, not a point-in-time snapshot. Concurrent events can change aggregate values between requests.

## Classification

- **Type:** Experimental refactor, Elasticsearch index/query change, additive API pagination behavior
- **Affected areas:** Backend/API, Elasticsearch index configuration and reindexing, event/stack search, pagination headers, configuration, telemetry, integration tests, performance tests
- **OpenSpec justification:** This changes persisted Elasticsearch index settings, replaces a compatibility-sensitive aggregation/filtering path, introduces a new raw ES|QL execution boundary, and changes how public pagination links are produced.

## Current implementation context

`EventHandler.GetInternalAsync` implements the four stack modes by requesting a `terms` aggregation on `stack_id`. The bucket size grows with `skip + limit + 1`, the handler skips buckets in memory, fetches stack documents by id, and joins them to aggregation buckets. Stack-only predicates are handled by `EventStackFilterQueryBuilder`, which first searches the stack index, materializes up to 20,000 stack ids, and injects those ids into the event query. This makes deep paging increasingly expensive and creates an explicit document-limit failure mode.

`StackHandler.GetInternalAsync` is a separate stack-only path. It pages the versioned stack index with page/limit and optionally runs daily-event aggregations for summary values. The initial lookup-join experiment must not force stack-only reads through daily event indices when no event-derived ordering or metrics are needed.

## Compatibility boundaries

- Preserve all existing event and stack routes.
- Preserve the `StackSummaryModel` response body and pagination headers.
- Preserve Lucene-style Exceptionless filter syntax and the existing split between event fields and stack fields, including special fields such as `fixed`, `regressed`, and `hidden`.
- Preserve free/premium filter validation and suspended-organization behavior before any ES|QL query executes.
- Preserve stack-mode rejection of explicit `sort`.
- Preserve page/limit behavior as a fallback during the experiment.
- Do not expose ES|QL syntax as a public API contract.
- Do not make a cursor token a durable SDK contract beyond being opaque and reusable with the same query.

## Non-goals

- Replacing ordinary event document search-after queries.
- Replacing direct stack-only repository searches with ES|QL when no event aggregation is required.
- Changing the documented Exceptionless filter language.
- Fixing or broadening mixed event/stack boolean filter semantics beyond current behavior.
- Providing point-in-time snapshot pagination for mutable aggregates.
- Raising the ES|QL cluster result limit.
- Supporting cross-cluster event indices in the first experiment.
- Removing the legacy aggregation implementation before correctness and performance gates pass.

## Rollback and mitigation

- Guard the ES|QL path with an operational feature flag/kill switch, disabled by default outside explicitly selected environments during the experiment.
- Route page-number requests and unsupported/capability-mismatch cases through the existing implementation.
- Keep the stack alias on the lookup-mode versioned index after an application rollback; normal repository reads and writes remain supported on that index mode.
- If lookup-index migration cannot complete, do not switch the versioned alias and keep the feature disabled.
- Do not silently retry an invalid user filter through the legacy path. Fallback is for capability/readiness/transient execution failures that are separately logged and measured.

## Success criteria

The experiment is eligible for follow-up production work only if it:

- matches the legacy result ids, ordering, summary metrics, filters, and totals for representative fixtures;
- traverses forward and backward without duplicates for a stable fixture, including tied aggregate values;
- removes the `skip + limit` terms-bucket growth and stack-filter id materialization from the primary rollup query;
- demonstrates no material latency or allocation regression at shallow pages and a material improvement at deep pages on representative daily-index data; and
- can be disabled without an index rollback or public API break.
