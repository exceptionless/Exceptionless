# Spec: Search and Stacks

## MODIFIED Requirements

### Requirement: Stack rollup modes preserve existing summaries and fixed ordering

Event list endpoints executed in `stack_recent`, `stack_frequent`, `stack_new`, or `stack_users` mode MUST preserve the existing `StackSummaryModel` response contract, mode selection semantics, filters, authorization, plan enforcement, and fixed mode ordering.

#### Scenario: Most frequent stacks

Given matching events span one or more daily indices
And their stacks exist in the active versioned stack index
When an authorized caller requests `mode=stack_frequent`
Then results are ordered by summed occurrence count descending
And ties are ordered by stack id ascending
And every summary contains the same metrics and canonical stack fields as the legacy implementation.

#### Scenario: Most recent stacks

Given matching events span one or more daily indices
When an authorized caller requests `mode=stack_recent`
Then results are ordered by the latest matching event date descending
And ties are ordered by stack id ascending.

#### Scenario: New stacks

Given stacks have first occurrences both inside and outside the resolved time range
When an authorized caller requests `mode=stack_new`
Then only stacks selected by the existing stack-first-occurrence time behavior are returned
And results are ordered by first occurrence descending with stack id ascending as the tie-breaker.

#### Scenario: Stacks by users

Given matching events contain repeated and distinct user identities
When an authorized caller requests `mode=stack_users`
Then results are ordered by the existing approximate distinct-user metric descending
And ties are ordered by stack id ascending.

#### Scenario: Explicit sort remains invalid

Given a request uses any stack rollup mode
When it supplies an explicit `sort`
Then the API returns the existing bad-request behavior rather than overriding the mode sort.

### Requirement: Stack rollup filters preserve Exceptionless query behavior

Stack rollup execution MUST preserve the current Lucene-style filter contract and MUST apply event and stack predicates without widening authorization or returning soft-deleted stacks.

#### Scenario: Event-only filter

Given a valid filter references only event fields
When a stack rollup query executes
Then only events matching that filter contribute to stack metrics.

#### Scenario: Stack-only filter

Given a valid filter references stack status, tags, title, references, fixed state, regressed state, hidden state, or stack identifiers
When a stack rollup query executes
Then only stacks matching the equivalent current stack-filter behavior are returned.

#### Scenario: Mixed event and stack filter

Given a filter contains event and stack fields in a form supported by the current filter visitors
When a stack rollup query executes
Then its result ids match the legacy implementation for the same fixture.

#### Scenario: Unsupported lossless translation

Given a validated filter cannot be represented by the lookup-join query without changing semantics
When the experimental path evaluates the request
Then the whole request uses the legacy implementation
And no partial filter is executed.

#### Scenario: Deleted or missing stack

Given an event references a soft-deleted or missing stack document
When a stack rollup query executes
Then that stack is not returned.

#### Scenario: Authorization is applied before aggregation

Given a caller can access only selected organizations or projects
When a stack rollup query executes
Then inaccessible event documents do not contribute to aggregation metrics
And a joined stack field is not the sole authorization check.

## ADDED Requirements

### Requirement: Stack rollup modes support opaque before and after cursors

Cursor-based stack rollup responses MUST define a deterministic total order and MUST provide opaque cursors that are valid only for the same logical query.

#### Scenario: Forward traversal

Given more matching stacks than the requested limit
When the caller follows each `after` link
Then each stable-fixture stack is returned exactly once in canonical mode order
And the last page does not advertise a next page.

#### Scenario: Backward traversal

Given the caller is on a page after the first page
When the caller follows the `before` link
Then the preceding items are returned in canonical mode order
And they match the page previously observed for the stable fixture.

#### Scenario: Equal primary sort values

Given multiple stacks have the same primary mode metric
When the caller traverses pages whose boundary falls within those ties
Then stack id provides a stable tie-breaker
And no tied stack is skipped or duplicated for the stable fixture.

#### Scenario: Relative time range is frozen

Given the initial request uses a relative time expression
When the caller follows a cursor after wall-clock time advances
Then the cursor uses the absolute UTC range resolved for the initial request.

#### Scenario: Concurrent aggregate changes

Given matching events or stack status change between cursor requests
When the caller follows a cursor
Then the API maintains its deterministic keyset comparison
But it does not promise point-in-time snapshot traversal.

#### Scenario: Malformed cursor

Given `before` or `after` is malformed, has an unknown version, or contains an invalid typed sort value
When the request is validated
Then the API returns `400 Bad Request`
And does not execute either Elasticsearch path.

#### Scenario: Cursor reused with another query

Given a valid cursor was issued for one scope, filter, time range, mode, or sort contract
When it is reused with a different logical query
Then the API returns `400 Bad Request`.

#### Scenario: Conflicting pagination parameters

Given a request supplies both `before` and `after`, or supplies `page` with either cursor
When the request is validated
Then the API returns `400 Bad Request`.

### Requirement: Lookup-join execution requires a ready single-shard lookup index

The experimental lookup-join path MUST execute only when the active stack alias resolves to one concrete lookup-mode index with one primary shard.

#### Scenario: Ready lookup index

Given the feature is enabled
And the stack alias resolves to one concrete index with `index.mode=lookup` and one primary shard
When a representable cursor stack-rollup request executes
Then the ES|QL lookup-join path may be used.

#### Scenario: Multi-shard or standard-mode stack index

Given the active stack index is not lookup mode or does not have exactly one primary shard
When a stack-rollup request executes
Then the legacy implementation is used
And readiness telemetry identifies the bounded reason.

#### Scenario: Alias resolves to multiple concrete indices

Given the stack alias resolves to multiple concrete indices
When a stack-rollup request executes
Then the lookup-join path is not used.

#### Scenario: Lookup feature disabled

Given the operational feature flag is disabled
When a stack-rollup request executes
Then existing legacy behavior is preserved without requiring an index rollback.

### Requirement: Page-number stack rollups remain compatible during the experiment

Explicit page-number requests MUST continue to use the existing page/limit behavior while cursor results are evaluated.

#### Scenario: Explicit page request

Given a request supplies `page` and `limit` without a cursor
When a stack rollup query executes
Then it returns the same result and page-link contract as the legacy implementation.

#### Scenario: Cursor request avoids offset-sized buckets

Given a cursor request follows a deep result boundary
When the lookup-join query executes
Then its final row limit is based on `limit + 1`
And it does not size a terms aggregation using the historical page offset.

### Requirement: Lookup-join failures are observable and safely classified

Lookup-join execution MUST preserve public validation and authorization errors, support cancellation and timeouts, and emit bounded operational telemetry for fallback.

#### Scenario: Invalid filter remains invalid

Given a user submits an invalid search filter
When query validation fails
Then the API returns the existing invalid-filter response
And does not retry through another implementation.

#### Scenario: Transient lookup execution failure

Given the lookup path is enabled and a classified transient Elasticsearch failure occurs
When the request can safely use the legacy implementation
Then the request falls back once
And telemetry records a bounded failure category without raw filters or cursors.

#### Scenario: Request cancellation

Given the HTTP request is cancelled
When ES|QL is executing
Then cancellation is propagated
And the handler does not start an unbounded fallback query.

### Requirement: Lookup-mode migration preserves canonical stack repository behavior

Migrating the versioned stack index to lookup mode MUST preserve active and soft-deleted stack documents and ordinary stack repository operations.

#### Scenario: Versioned reindex completes

Given an existing versioned stack index contains active and soft-deleted documents
When the lookup-mode version is built and validated
Then all documents and required mappings are present before the alias switches atomically.

#### Scenario: Repository operations after cutover

Given the stack alias points to the lookup-mode index
When Exceptionless reads, saves, patches, or soft-deletes a stack through `IStackRepository`
Then behavior matches the existing repository contract.

#### Scenario: Migration is not ready

Given reindex validation fails or the lookup index is not ready
When startup index configuration runs
Then the alias is not switched
And the lookup-join feature remains disabled.

### Requirement: Experimental performance is measured against the legacy path

The experimental PR MUST include a repeatable comparison using representative multi-day data before the lookup path is proposed as the default.

#### Scenario: Correctness comparison

Given identical representative fixtures
When legacy and lookup-join queries run for every mode, filter family, and measured page depth
Then result ids, order, summaries, and requested totals match.

#### Scenario: Shallow and deep measurements

Given repeated warm benchmark runs
When first-page and deep-page cases are measured
Then the report includes median and p95 wall time, allocations or response bytes where available, and failure/circuit-breaker counts
And no conclusion is based on a single run.

#### Scenario: Promotion gate fails

Given correctness differs, shallow latency materially regresses, or representative lookup joins create unacceptable heap/circuit-breaker pressure
When the experiment is reviewed
Then the feature remains disabled or the experiment is abandoned without removing the legacy path.
