# Design: ES|QL Lookup-Join Stack Pagination Experiment

## Decision

Introduce a narrow `IStackRollupSearchService` for the four event stack-rollup modes. The service may use `ElasticsearchClient` directly because Foundatio.Repositories does not expose ES|QL, while all ordinary stack hydration, project lookup, writes, and direct stack searches remain repository-backed.

The query joins before aggregating:

```text
FROM <event-alias>
| KEEP stack_id, count, date, <physical-user-field>
| RENAME <physical-user-field> AS event_user
| LOOKUP JOIN <single-concrete-target-stack-alias>
  ON stack_id == id AND is_deleted == false
  AND QSTR(<stack predicate>, {"default_operator": "AND"})
| WHERE id IS NOT NULL
| STATS users = COUNT_DISTINCT(event_user),
        total = SUM(COALESCE(count, 1)),
        first_occurrence = MIN(date),
        last_occurrence = MAX(date)
  BY stack_id
| INLINE STATS total_stacks = COUNT(*)
| WHERE <optional keyset cursor predicate>
| SORT <mode metric direction>, stack_id ASC
| LIMIT <limit + 1>
```

The localhost Elasticsearch 9.5 capability spike rejected `QSTR` after `STATS` with `verification_exception: [QSTR] function cannot be used after STATS`. Joining before aggregation is therefore required to preserve the existing Lucene-style stack filter contract without translating that language into a second expression language. This removes the 20,000-stack-id materialization and offset-sized terms buckets from the primary rollup query, but the lookup processes matching event rows rather than one aggregated row per stack. The existing project-level `TotalUsers` helper remains repository-backed and can still invoke the legacy active-stack filter on a cache miss; replacing that auxiliary query is a separate follow-up. Those costs are explicit performance gates for the experiment.

The spike also proved two less obvious constraints. Real event and stack mappings both expose fields such as `id`, so the event input must be projected with `KEEP` and renamed before the join or ES|QL rejects later references as ambiguous. In addition, `QSTR` defaults adjacent clauses to `OR`, while Exceptionless's repository parser treats them as implicit `AND`; every joined stack predicate therefore supplies `default_operator=AND`.

Authorization, project, event-filter, and date predicates are supplied through the ES|QL REST request's Query DSL `filter`, built by the existing Foundatio event query builder. The prototype currently names the canonical event alias and relies on this pushdown filter. Resolving only the concrete daily indices remains a performance follow-up because a requested day can legitimately have no concrete index and ES|QL does not inherit the repository client's ignore-unavailable behavior.

The exact physical event and stack field names must come from the index configuration rather than duplicated string literals. User values must be sent as ES|QL parameters; only allow-listed command fragments and validated index names may be composed into query text.

## Existing paths and scope

### Event stack-rollup path

`EventHandler.GetInternalAsync` currently:

1. validates with `EventStackQueryValidator`,
2. builds a Foundatio daily-event query,
3. lets `EventStackFilterQueryBuilder` search the stack index and inject up to 20,000 ids,
4. requests a terms aggregation sized to `skip + limit + 1`,
5. skips buckets in memory,
6. hydrates stack documents, and
7. joins aggregation metrics to `StackSummaryModel`.

The experiment replaces steps 3 through 5 for cursor requests. Stack hydration and formatting remain shared with the current code.

### Direct stack endpoint path

`StackHandler.GetInternalAsync` searches only the versioned stack index and supports arbitrary stack sort expressions. It should continue using `IStackRepository`. A follow-up can add Foundatio search-after/before tokens with an appended `id` tie-breaker. Sending these stack-only reads through all daily event indices would add cost and change zero-event/time-range semantics without gaining anything from `LOOKUP JOIN`.

### Legacy page path

If `page` is present, retain the current aggregation/page implementation for compatibility and A/B comparison. Cursor and page parameters are mutually exclusive. The experiment must collect telemetry that distinguishes lookup-join, legacy-page, and fallback execution.

## Index topology and migration

Events remain in the existing `DailyIndex<PersistentEvent>` indices. The ES|QL source must reuse the same start/end index resolution as `.Index(utcStart, utcEnd)` so it does not scan outside retention or include unrelated indices. ES|QL requires all selected shards to be available, so missing/closed daily indices need an explicit no-data or fallback policy rather than a broad wildcard.

The current prototype deliberately uses the canonical event alias plus an exact Query DSL date filter until that missing-index policy is implemented. The benchmark must include the shard-fan-out cost of this choice; it is not production-ready evidence for the final daily-index resolution requirement.

The canonical `StackIndex` is a `VersionedIndex<Stack>`. Create its next version with:

- `index.mode = lookup`,
- exactly one primary shard, regardless of the general `ElasticsearchOptions.NumberOfShards`,
- the configured replica count, and
- the existing mapping and alias.

The versioned alias must resolve to exactly one concrete index before the feature is considered ready. Reindex all active and soft-deleted stack documents, validate document counts and representative mappings, then atomically switch the alias using the existing versioned-index migration mechanism.

Lookup mode's one-primary-shard constraint is the central capacity tradeoff. Stacks are much smaller than events, but the experiment must measure shard size, document count, indexing/update throughput, patch latency, and query heap. Deployments that cannot fit their stack corpus or write rate on one primary shard must keep the feature disabled; the initial experiment must not introduce a dual-write lookup projection without a separate design.

`index.mode` is a final creation-only setting. The stack index config applies lookup mode only while creating v2 and overrides Foundatio's existing-index settings update to send only mutable replica/priority settings; otherwise every later startup receives a harmless-looking but rejected `PUT _settings` request.

Normal `IStackRepository` reads, writes, scripts, and deletes continue against the lookup-mode index. Replicas remain available for resilience. Log a clear startup readiness state with alias target, lookup mode, and primary shard count, without logging credentials or query values.

## Filter preservation

Continue using `EventStackQueryValidator` and `EventStackFilter` so the public Lucene-style filter contract and premium-feature classification remain unchanged.

- Resolve the event-only filter through the current visitor and apply it immediately after `FROM`, using `QSTR` or the equivalent generated Query DSL filter.
- Resolve the stack filter through the current stack visitor, including physical field rewrites for `fixed`, `regressed`, `hidden`, and `stack_id`.
- Apply the stack predicate in the `LOOKUP JOIN` condition together with `stack_id == id`, before `STATS`. Elasticsearch 9.5 permits `QSTR` in this position but rejects it after aggregation; every supported filter fixture must still be proven.
- Require a non-null joined stack id and exclude soft-deleted stacks after the join, giving the left join effective inner-join behavior.
- Preserve the current handling of `@!`; do not reinterpret it as part of this refactor.
- Preserve organization/project authorization as an event-source predicate before `STATS`. Never depend on a joined stack field as the only authorization boundary.

If a validated filter cannot be represented by the 9.5 query, route that request to the legacy implementation and increment a reason-labelled fallback metric. Do not partially apply a filter.

## Mode semantics

Use one query shape and an allow-listed mode definition:

| Mode | Primary sort | Selection detail |
| --- | --- | --- |
| `stack_recent` | `last_occurrence DESC` | latest matching event in the resolved range |
| `stack_frequent` | `total DESC` | sum event `count`, preserving the existing default occurrence behavior |
| `stack_new` | `first_occurrence DESC` | preserve the existing stack first-occurrence range predicate added by `AddFirstOccurrenceFilter` |
| `stack_users` | `users DESC` | approximate distinct user count, matching current cardinality semantics |

Every mode appends `stack_id ASC` as a tie-breaker. Sort is applied after `LOOKUP JOIN` because Elasticsearch does not preserve a sort performed before a lookup join.

Hydrate canonical stack documents by returned ids and explicitly restore ES|QL row order before formatting summaries. Missing stack documents are excluded and measured; they must not reorder the remaining page.

## Cursor contract

Use a versioned, base64url-encoded JSON cursor containing:

- cursor schema version,
- mode and canonical sort direction,
- typed primary sort value,
- stack id tie-breaker,
- resolved absolute UTC start/end,
- a hash of organization/project scope, normalized filter, mode, and sort contract, and
- direction (`before` or `after`) if required by the decoder.

Do not include user identity, tokens, credentials, raw authorization data, or unbounded filter text. Reject unknown versions, malformed values, non-finite numeric values, and fingerprint mismatches with `400 Bad Request`.

For canonical descending order `(metric DESC, stack_id ASC)`:

- `after`: `metric < anchor OR (metric == anchor AND stack_id > anchor_id)`
- `before`: `metric > anchor OR (metric == anchor AND stack_id < anchor_id)`

A `before` query reverses both sort directions, takes `limit + 1`, and reverses the selected items before returning them. Equivalent predicates must be generated for ascending sorts if reused later. Aggregated mode metrics must be non-null; direct stack sorts need an explicit null policy before they can reuse this cursor.

The cursor freezes a relative time expression to its initially resolved absolute range. It does not freeze mutable event aggregates. New/backfilled events or stack status changes can move a stack across the anchor between requests; this is documented as live keyset pagination rather than snapshot pagination.

## Totals and limits

The existing `include=total` behavior must remain available. Because a cursor query limits rows after grouping, total count must be computed before the cursor predicate. Elasticsearch 9.5 supports `INLINE STATS total_stacks = COUNT(*)` between grouping and the cursor predicate, which the localhost spike proved returns the pre-cursor total on every selected row. Compare its result with the current cardinality behavior in tests and omit it when total is not requested. Do not increase `esql.query.result_truncation_max_size`.

The ES|QL 10,000-row output limit does not prevent this design because the final output is `limit + 1`; `STATS` still processes the full selected source. The service must retain the API's existing limit clamp.

## Capability, fallback, and errors

Enable lookup-join execution only when:

- the operational feature flag is enabled,
- Elasticsearch reports a compatible 9.5 capability,
- the stack alias resolves to one concrete index,
- that index has `index.mode=lookup` and one primary shard, and
- the query is cursor-based and representable without semantic loss.

Fallback to the legacy query for feature-off, readiness mismatch, explicitly unsupported filters, and classified transient/circuit-breaker failures. Emit structured logs and counters with a bounded reason label. Invalid filters, invalid cursors, authorization failures, and plan-limit failures must retain their public error and must not be hidden by fallback.

Use a timeout and cancellation token. Record duration, selected daily-index count, returned rows, mode, direction, fallback reason, and response allocation/size where available. Never log raw user filters or cursor contents at normal log levels.

## Performance experiment

Build a repeatable integration benchmark fixture with:

- multiple daily event indices,
- at least tens of thousands of stacks for CI-scale measurement and an optional larger local profile,
- skewed occurrence/user distributions,
- ties on every mode metric,
- active, fixed, regressed, ignored, and soft-deleted stacks, and
- both event-only and stack-only filters.

Compare legacy and ES|QL paths at the first page and equivalent depths near pages 100, 500, and the current maximum skip. Capture median and p95 wall time across repeated warm runs, request/response bytes, managed allocations, Elasticsearch took time if exposed, and failures/circuit-breakers. A noisy single run is not sufficient evidence.

Promotion gate: result equivalence must be exact for ids/order/summary fields, shallow-page p95 must not materially regress, and deep-page work must remain bounded by `limit` instead of requested offset. If lookup-join heap/circuit-breaker behavior is worse for representative data, keep the experiment behind the flag or abandon it.

## Official Elasticsearch references

- [LOOKUP JOIN command](https://www.elastic.co/docs/reference/query-languages/esql/commands/lookup-join)
- [LOOKUP JOIN prerequisites and limitations](https://www.elastic.co/docs/reference/query-languages/esql/esql-lookup-join)
- [Index mode settings](https://www.elastic.co/docs/reference/elasticsearch/index-settings/index-modules)
- [ES|QL SORT and tie-breakers](https://www.elastic.co/docs/reference/query-languages/esql/commands/sort)
- [ES|QL LIMIT behavior](https://www.elastic.co/docs/reference/query-languages/esql/commands/limit)
- [ES|QL QSTR](https://www.elastic.co/docs/reference/query-languages/esql/functions-operators/search-functions/qstr)
- [ES|QL REST API](https://www.elastic.co/docs/reference/query-languages/esql/esql-rest)
