# Tasks: ES|QL Lookup-Join Stack Pagination Experiment

## Design and capability spike

- [x] 1. Prove the Elasticsearch 9.5 query shape against local Aspire Elasticsearch
  - Create a temporary daily-event fixture and a single-shard lookup-mode stack index.
  - Prove and document that stack `QSTR` requires `LOOKUP JOIN` before `STATS` in Elasticsearch 9.5.
  - Prove the stack alias can be used when it resolves to exactly one concrete lookup index.
  - Prove stack `QSTR`/Lucene-pushable predicates in the join condition, soft-delete exclusion, aggregate metrics, exact pre-cursor totals, tied forward cursors, and reverse-before ordering.
  - Verification: localhost-only REST fixture on isolated Elasticsearch 9.5 port `9215`; no production endpoint.

- [ ] 2. Add lookup-join capability/readiness detection and an operational kill switch
  - Detect feature flag, server capability, alias target count, lookup mode, and primary shard count.
  - Add bounded reason-labelled telemetry for disabled/unready/fallback states.
  - Verification: targeted tests for ready, feature-off, standard-mode, multi-target alias, and wrong-shard states; `dotnet test --project tests/Exceptionless.Tests -- --filter-class <CapabilityTestClass>`.

## Stack index migration

- [ ] 3. Create the next version of `StackIndex` in lookup mode
  - Set `index.mode=lookup`, force one primary shard, retain configured replicas and current mapping/analysis.
  - Warn when the general shard configuration is greater than one because stack lookup mode overrides it.
  - Verification: index configuration test asserts mode, one primary, replicas, mapping aliases, and analyzer preservation.

- [ ] 4. Validate versioned reindex and alias cutover
  - Reindex active and soft-deleted stack documents.
  - Verify counts and representative documents before atomic alias switch.
  - Verify normal `IStackRepository` get/save/patch/delete behavior on the lookup-mode index.
  - Verification: `dotnet test --project tests/Exceptionless.Tests -- --filter-class StackRepositoryTests` plus a dedicated migration integration test.

## Query service and filters

- [ ] 5. Add a narrow ES|QL stack-rollup search service
  - Isolate direct client use behind `IStackRollupSearchService`.
  - Reuse the daily-index resolver and pass values as parameters.
  - Apply authorization/project/time predicates before aggregation.
  - Verification: service integration tests over at least three daily indices and cancellation/timeout tests.

- [x] 6. Preserve current event/stack filter semantics
  - Reuse `EventStackQueryValidator` and `EventStackFilter` visitors.
  - Apply event-only predicates at the source and stack predicates in the lookup join.
  - Preserve special fields, `@!`, soft-delete exclusion, premium classification, and no-partial-filter fallback.
  - Verification: port the full `CheckStackModeCounts`, deleted-stack, premium-search, mixed field, wildcard, range, missing/exists, and invalid-filter matrices to run against legacy and ES|QL paths.

- [x] 7. Implement mode aggregation and ordered hydration
  - Implement recent/frequent/new/users metrics and fixed mode sorts.
  - Append `stack_id ASC` to every sort.
  - Hydrate through repositories and restore ES|QL row order before formatting.
  - Verification: parity tests assert ids, order, summary metrics, tags, project names, and missing-stack handling for every mode.

## Cursor pagination and API integration

- [ ] 8. Add versioned query-bound stack-rollup cursors
  - Encode typed metric, stack id, resolved UTC range, mode/sort, fingerprint, and version.
  - Implement forward and reverse keyset predicates and reverse-before result handling.
  - Reject malformed, mismatched, dual-direction, and page-plus-cursor requests.
  - Verification: unit tests for all modes, tied values, forward/back traversal, token version/type validation, relative time freezing, and tampering/mismatch errors.

- [x] 9. Route cursor stack modes through ES|QL while preserving page fallback
  - Keep explicit `page` requests on the legacy path.
  - Preserve `sort` rejection and response body/status behavior.
  - Populate `before`/`after` pagination links with opaque cursors.
  - Verification: `dotnet test --project tests/Exceptionless.Tests -- --filter-class EventEndpointTests` and update `tests/http/*.http` for the additive cursor examples.

- [ ] 10. Preserve optional total behavior
  - Compute total before cursor filtering only when requested.
  - Compare lookup-join total semantics with current cardinality behavior.
  - Verification: endpoint tests for no total, first/middle/last page total, empty results, filters, and total parity.

- [ ] 11. Add cursor paging to direct stack-only endpoints using Foundatio search-after, if included in the experimental PR
  - Do not route stack-only reads through ES|QL.
  - Preserve arbitrary stack sort and append a deterministic id tie-breaker.
  - Keep explicit page requests compatible.
  - Verification: `dotnet test --project tests/Exceptionless.Tests -- --filter-class StackEndpointTests` with every supported stack sort, ties, null policy, before/after, and page fallback.

## Failure handling, performance, and completion

- [ ] 12. Add classified fallback and observability
  - Fall back only for feature/readiness/unsupported/transient categories.
  - Preserve invalid filter/cursor, authorization, and plan errors.
  - Record mode, direction, duration, selected index count, row count, and bounded fallback reason without raw filters/cursors.
  - Verification: fault-injection tests for timeout, cancellation, circuit breaker, invalid query, and feature-off behavior.

- [ ] 13. Add repeatable legacy-versus-ES|QL benchmark coverage
  - Seed multiple daily indices, skewed metrics, ties, statuses, deletions, and large stack cardinality.
  - Measure repeated warm first/deep pages and report median/p95, allocations, bytes, and failures.
  - Verification: committed benchmark command/script and a checked-in results template; run locally against Aspire and attach measurements to the experimental PR.

- [x] 14. Run targeted verification and strict OpenSpec validation
  - `dotnet build`
  - `dotnet test --project tests/Exceptionless.Tests -- --filter-class EventEndpointTests`
  - `dotnet test --project tests/Exceptionless.Tests -- --filter-class StackEndpointTests`
  - `dotnet test --project tests/Exceptionless.Tests -- --filter-class EventStackFilterQueryTests`
  - localhost-only cursor smoke test through Aspire
  - `openspec validate experiment-esql-lookup-join-stack-pagination --strict --no-interactive`
