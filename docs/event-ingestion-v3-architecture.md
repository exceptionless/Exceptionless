# Event ingestion V3 architecture

This document records the implementation decisions and performance baseline
methodology for [issue #2368](https://github.com/exceptionless/Exceptionless/issues/2368).
It is deliberately separate from the public API documentation until the V3
contract is ready for rollout.

## Objectives

Event ingestion V3 is a breaking, performance-first API with these invariants:

- ASP.NET Core Minimal APIs own the HTTP boundary. V3 does not use an MVC
  controller or MVC input formatter.
- Clients send independent JSON objects as they capture events. They never
  construct a top-level JSON array.
- The request body is consumed incrementally from a `PipeReader` with
  source-generated System.Text.Json metadata.
- The API processes bounded microbatches inline. A successful response means
  that every acknowledged event is durable or reached another terminal success
  state such as discarded or duplicate.
- The primary event path does not write the request to object storage and does
  not enqueue it for another process to deserialize.
- Clients send raw stack traces. The server computes the grouping fingerprint
  and builds the persisted structured error.
- Events assigned to discarded stacks terminate before billable admission,
  full error materialization, event persistence, stack statistics, or side
  effects.
- V3 correctness is scale-out safe and never depends on sticky sessions or
  process-local authoritative state.
- V2 remains contract-compatible. V3 does not pay a compatibility tax to share
  an implementation with V2.

## Current V2 flow

The V2 path is intentionally durable before processing, but that durability
requires the payload to cross several boundaries:

1. `EventController.PostAsync` resolves the project and wraps the request body
   with `EventPostRequestBodyStream`.
2. `EventPostService.SaveAndEnqueueAsync` writes the payload to file storage and
   enqueues an `EventPost` pointer.
3. `EventPostsJob` downloads the complete payload into a byte array.
4. Compressed payloads are expanded into a second complete byte array.
5. The job parses the complete body into a collection of `PersistentEvent`
   objects.
6. `EventPipeline` runs a reflection-discovered action list.
7. `PipelineBase` creates a filtered context list for every action.
8. `AssignToStackAction` resolves or creates stacks and only then detects the
   discarded status.
9. `SaveEventAction` persists events, followed by statistics, notifications,
   counters, and processed plugins.

Plan admission occurs before stack assignment in both `OverageMiddleware` and
`EventPostsJob`. A request can therefore be rejected before the system learns
that its events belong to a free discarded stack.

## V3 processing boundary

The V3 endpoint is a transport adapter over an explicit, batch-oriented Core
processor. For each bounded microbatch, the processor performs these steps in
order:

1. Validate the minimal event envelope.
2. Apply retention and nonbilling safety rules.
3. Compute canonical stack fingerprints.
4. Resolve unique stack routes in bulk.
5. Terminate discarded events and record an aggregate discard count.
6. Reserve billable quota for survivors only.
7. Materialize complete events and structured errors.
8. Resolve or create missing stacks.
9. Persist events and durable side-effect intent in bulk.
10. Commit or release billable reservations.

The endpoint stops reading while a full microbatch is processed. This bounds
memory and lets HTTP/TCP flow control apply backpressure. Reading and processing
may overlap through a channel with capacity one only if benchmarks demonstrate
a material benefit.

## Decision: inline acknowledgement

V3 acknowledges after the primary event write, not after a queue write. This
removes file-storage I/O, queue I/O, worker scheduling, a payload download, and
a second deserialization from the normal path.

If durable storage is unavailable, V3 fails quickly with a retryable response.
It does not enqueue after processing begins because a partially successful
write makes that fallback ambiguous. A separate, explicit buffered mode is
required if the product must acknowledge while the event store is unavailable.

## Decision: bounded stream segments

An indefinitely open upload cannot provide a reliable acknowledgement boundary.
Official clients therefore stream events immediately into a request segment and
close the segment when a configured count, byte, or elapsed-time limit is
reached. The client retains only the unacknowledged segment and replays it after
a connection loss or retryable response.

This is streaming rather than array batching: each event is serialized once and
written immediately as an independent top-level JSON value.

## Decision: deterministic idempotency

Every V3 event carries a stable client event identifier. The server derives a
deterministic storage identity scoped to the project and uses create-only
semantics. A duplicate create is a terminal success.

If a response is lost after persistence, the client replays the complete segment
with the same identifiers. Previously stored events become duplicate successes;
new events continue normally. Side-effect intent uses the same deterministic
identity so replay cannot duplicate notifications or webhooks.

## Decision: discarded-stack fast path

Discard classification requires the same canonical fingerprint used by stack
assignment. V3 computes that fingerprint from a minimal event representation
before building the complete `PersistentEvent`, `Error`, and `StackFrame`
object graph.

Stack routing uses a lightweight value containing stack identifier, status, and
a cache schema version. Resolution is deduplicated within the microbatch, read
from the distributed cache in bulk, and falls back to a projected repository
query for cache misses.

The distributed cache is authoritative initially. A process-local positive
discard cache is unsafe without proven versioning and invalidation because a
stale entry would silently discard events after a stack is reopened.

A discarded event performs none of the following:

- Billable quota reservation.
- Full error or stack-frame materialization.
- Event indexing.
- Stack occurrence updates.
- Notification, webhook, archive, enrichment, or outbox writes.

Discarded events still count toward protective byte, request, and event-rate
limits. Those controls prevent abuse and are not customer billing.

## Decision: quota reservation and settlement

The existing read-then-process usage check is not an adequate scale-out
reservation primitive. V3 separates protective admission from billable usage
and uses three billable operations:

- Reserve capacity for non-discarded survivors.
- Commit capacity for successfully persisted events.
- Release capacity for invalid events, duplicates, cancellations, or failed
  writes.

The distributed operation must be atomic across API instances. A customer at
the billable limit can still submit an event assigned to an already-discarded
stack, while active and new-stack events are blocked.

## Decision: side-effect durability

The primary event is authoritative. Notifications, webhooks, archive export,
nonessential geolocation, and repairable derived statistics remain outside the
request critical path.

The inline writer persists deterministic side-effect or repair intent before
acknowledgement. Consumers are idempotent. Partial bulk outcomes are reconciled
by deterministic identifiers rather than by placing the original event payload
back on the ingestion queue.

## V2 compatibility boundary

V2 retains its routes, payloads, 202 response, controller, and durable queue.
Shared services are extracted below the transport boundary:

- Canonical fingerprinting.
- Stack route resolution.
- Server-side stack parsing.
- Batch stack resolution and creation.
- Batch event persistence.
- Usage accounting primitives.

V3 must not instantiate V2 queue models, plugin contexts, or compatibility
converters. V2 may adopt a shared service only when its compatibility fixtures
remain unchanged and the change is neutral or positive in measurements.

## Baseline methodology

All comparisons use the same commit, machine, runtime configuration,
Elasticsearch topology, Redis topology, payload corpus, compression setting,
and client concurrency. Record the following environment information with every
result:

- Commit SHA and configuration.
- .NET SDK and runtime versions.
- Operating system and processor topology.
- Elasticsearch and Redis versions/resources.
- Number of API and job instances.
- Payload size distribution and stack-signature cardinality.
- Client concurrency and connection reuse.

Measure V2 and V3 separately for these scenarios:

- One hot active stack.
- High-cardinality active stacks.
- New stacks.
- 0%, 10%, 50%, 90%, and 100% discarded events.
- Small, median, large, and maximum events.
- Compressed and uncompressed requests.
- Healthy, slow, and unavailable Elasticsearch.
- One, two, four, and eight API instances.

Capture:

- Events per second and bytes per second.
- CPU time per event.
- Allocated bytes per event and Gen 0/1/2 collections.
- Working set and managed heap under a long stream.
- p50, p95, and p99 time from final event byte to durable acknowledgement.
- File-storage, queue, cache, and Elasticsearch operations per event.
- Stack-route cache hit and miss rates.
- Cost per million persisted and discarded events.

## Performance gates

- No request-sized byte array, string, `MemoryStream`, `JsonDocument`, or
  top-level event collection on V3.
- No large-object-heap allocation caused by normal stream framing or microbatch
  growth.
- Memory remains bounded by configured in-flight requests and microbatch limits
  during a long stream.
- A discarded event creates no complete error/frame graph and performs no event
  index write.
- At most one distributed route lookup per unique signature per microbatch.
- The normal V3 path performs no object-storage payload write/read and no
  primary ingestion queue operation.
- Scaling from one to four API instances reaches at least 80% efficiency until
  the shared datastore is the measured bottleneck.
- V3 reduces CPU, allocation rate, and time-to-durable-event relative to V2.
  Numeric reduction targets are set from the repeatable baseline rather than an
  unmeasured estimate.

## Verification requirements

Completion requires all of the following evidence:

- Unit tests for contracts, fingerprinting, parsing, routing, quota settlement,
  idempotency, and result aggregation.
- Integration tests for chunked NDJSON, compression, authentication, limits,
  discard behavior, persistence, replay, and failure responses.
- Multi-instance tests for route status changes and quota reservation.
- V2 serialization audit results before and after shared-service adoption.
- OpenAPI V3 baseline and HTTP samples.
- Microbenchmark reports and end-to-end load results for the scenario matrix.
- A staged rollout through the internal project and selected projects with a
  documented rollback switch.

## Implemented API and client behavior

V3 is exposed through `POST /api/v3/events` and
`POST /api/v3/projects/{projectId}/events`. Both are Minimal API routes and
require a client-scoped bearer token. The request content type is
`application/x-ndjson`; each event is one complete top-level JSON value. The
routes accept identity, gzip, and Brotli content encoding and reject top-level
arrays.

Clients must generate one stable `id` per captured event and reuse it whenever
the containing segment is replayed. They should also send the original event
`date`; when omitted, the server receipt time is used. The configured
`IdempotencyWindow` retains the server-date mapping for retries whose original
event omitted a date. `reference_id` remains a separate business identifier and
is not transport idempotency.

Clients write events to the HTTP request as they occur, but close and reopen the
segment at a bounded count, byte size, elapsed time, flush, or shutdown
boundary. They retain only the unacknowledged segment. A connection loss, 5xx,
or timeout replays the complete segment with the same event ids. A 200 response
returns terminal counts for `persisted`, `discarded`, `duplicate`, `blocked`,
and `invalid`. Earlier microbatches may already be durable when a later part of
the segment produces a request-level failure; replay remains safe.

Error clients send `exception_type` and the original `stack_trace`. The server
parses .NET, Java, JavaScript, and Python frame forms, preserves caused-by
chains, and stores structured frames. Unsupported formats use a normalized,
hashed raw-trace fallback and increment a parser-fallback metric without logging
the trace. Optional `stacking.signature_data` and `stacking.title` provide
explicit manual stacking.

The executable reference client and repeatable load harness live in
`benchmarks/Exceptionless.Ingestion.Load`; usage is documented in
`benchmarks/README.md`. Request examples live in `tests/http/events-v3.http`,
and the isolated OpenAPI document is available at `/docs/v3/openapi.json`.

## Configuration and rollout

`EventIngestionV3:Enabled` defaults to `false`. Rollout can be constrained with
`AllowedProjectIds` and `AllowedOrganizationIds`; empty sets allow every
authenticated project. Other controls are:

- `MicroBatchSize`, `MaximumMicroBatchBytes`, and `MaximumEventSize`.
- `MaximumCompressedBodySize`, `MaximumDecompressedBodySize`, and
  `MaximumEventsPerRequest`.
- `MaximumConcurrentRequests`, `ConcurrencyQueueLimit`, and
  `MaximumStackCreationConcurrency`.
- `RequestTimeout` and `IdempotencyWindow`.
- `StackRouteCacheDuration` and `NegativeStackRouteCacheDuration`.

Start with the internal Exceptionless project in the allowlist, compare V2 and
V3 stack signatures, terminal counts, usage, route-cache metrics, allocation
profiles, and datastore saturation, then expand the allowlists gradually.
Rollback is immediate: disable `EventIngestionV3:Enabled` and have clients use
the unchanged V2 endpoint. V2 retains its controller, queued payload handoff,
payload formats, and 202 response. Shared distributed quota primitives and
stack-route cache lifecycle improvements do not change that contract.

## Durable side effects

The primary event and deterministic side-effect work item are required before
acknowledgement. The background handler performs stack-usage repair,
request/environment/geolocation enrichment, notifications, webhooks, post-save
plugins, and deterministic archive export when archival is enabled. Work-item,
notification, and webhook identities are stable for the configured idempotency
window, so a client replay or work-item retry cannot enqueue the same
user-visible effect twice. Distributed completion markers are keyed per event
for stack statistics, notification/webhook enqueueing, and post-save plugins;
failed effects release only the markers they claimed so a retry can repair them
from the authoritative event documents.
