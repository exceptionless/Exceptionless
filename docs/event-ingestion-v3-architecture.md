# Event ingestion V3 architecture

This document records the implementation decisions and performance baseline
methodology for [issue #2368](https://github.com/exceptionless/Exceptionless/issues/2368).
It is deliberately separate from the public API documentation until the V3
contract is ready for rollout.

The companion [client protocol and ecosystem plan](/event-ingestion-v3-client-protocol/)
defines the minimum sender, capability profiles, Sentry and Raygun comparison,
and compatibility rules for future event and non-event telemetry.

## Objectives

Event ingestion V3 is a breaking, performance-first API with these invariants:

- ASP.NET Core Minimal APIs own the HTTP boundary. V3 does not use an MVC
  controller or MVC input formatter.
- Clients send independent JSON objects as they capture events. Each nonblank
  NDJSON line contains exactly one object; adjacent objects on one line are
  invalid, and only the final line may omit its newline. Clients never construct
  a top-level JSON array.
- The request body is consumed incrementally from a `PipeReader` with
  source-generated System.Text.Json metadata.
- The API processes bounded microbatches inline. A successful response means
  that every acknowledged event is durable or reached another terminal success
  state such as discarded or duplicate.
- The primary event path does not write the request to object storage and does
  not enqueue it for another process to deserialize.
- Clients send raw stack traces. The server computes the grouping fingerprint
  and builds the persisted structured error.
- A new language can implement the core sender using only its standard HTTPS
  and JSON facilities. Advanced reliability and framework features are
  independently testable capability profiles, not ingestion prerequisites.
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

1. Validate only the small identity and grouping envelope.
2. Compute canonical stack fingerprints.
3. Resolve unique stack routes in bulk.
4. Terminate known-discarded events and record an aggregate discard count.
5. Validate optional context for survivors and detect durable duplicates.
6. Apply retention and fixed-version nonbilling rules to new events.
7. Reserve billable quota for remaining new events only.
8. Materialize complete events and structured errors.
9. Resolve or create missing stacks.
10. Persist events and durable side-effect intent in bulk.
11. Commit or release billable reservations.

Each API replica applies two independent concurrency boundaries. A relatively
high active-stream guard bounds open HTTP requests for the replica and for each
organization. An idle stream holds only that stream permit; it does not consume
scarce processing capacity. When a microbatch is ready, the endpoint stops
reading and waits for the lower per-organization and global processing limits
before it calls the Core processor. This bounds memory and lets HTTP/TCP flow
control apply backpressure without letting slow producers starve ready work.
Active-stream admission is deliberately two-stage: a global permit is acquired
before the raw request-body limit is relaxed, then the endpoint acquires an
organization permit keyed by the organization of the authorized, routed project.
The second stage does not consume another global permit, and explicit-project
requests are never attributed to an unrelated default organization.

Both processing queues are bounded and ordered oldest first, with the
organization boundary applied before global admission. A full queue returns
`429`; if earlier microbatches in the same request are already durable, the
normal problem response includes their partial result and replay guidance.
Reading and processing may overlap through a channel with capacity one only if
benchmarks demonstrate a material benefit.

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

This is streaming rather than array batching: each event is serialized once,
written immediately as one NDJSON line, and followed by a newline before the
next event. The final newline is optional; concatenating adjacent JSON objects
without a line delimiter is invalid.

## Decision: bounded idempotency with date-routable identities

Every V3 event carries a stable client event identifier. The server derives a
date-routable storage identity from the project, client identifier, and supplied
event date, then uses create-only persistence semantics. This normal SDK path is
stateless and adds no per-event cache write. When `date` is omitted (or must be
clamped because it is in the future), the server instead atomically claims the
identity in a distributed mapping. Each mapping expires independently at the
configured idempotency window, keeping storage bounded, and preserves the exact
canonical event date and first receipt time so a retry across midnight cannot
route the write and lookup to different indexes. Benchmark processing-status
mode also claims mappings because its read-only endpoint accepts client ids,
not complete events. A duplicate create is a terminal success.

If a response is lost after persistence, the client replays the complete segment
with the same identifiers. Previously stored events become duplicate successes;
new events continue normally. Side-effect intent uses the same deterministic
identity. Durable queue markers suppress normal notification and webhook replay;
delivery remains at-least-once in the narrow case where a queue accepts an item
but its deduplication marker cannot be recorded.

## Decision: discarded-stack fast path

Discard classification requires the same canonical fingerprint used by stack
assignment. V3 computes that fingerprint from a minimal event representation
before building the complete `PersistentEvent`, `Error`, and `StackFrame`
object graph.

Stack routing uses a lightweight value containing stack identifier, status, and
a cache schema version. Resolution is deduplicated within the microbatch, read
from the distributed cache in bulk, and falls back to a projected repository
query for cache misses. Cold fills and authoritative mutations take one
project-generation lock and compare/write all affected entries in bulk, avoiding
a distributed lock and cache command chain for every distinct signature while
still preventing an older repository result from overwriting a newer status.

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
and uses three logical billable operations:

- Reserve capacity for non-discarded survivors.
- Commit capacity for successfully persisted events.
- Release capacity for invalid events, duplicates, cancellations, or failed
  writes.

The distributed operation must be atomic across API instances. A customer at
the billable limit can still submit an event assigned to an already-discarded
stack, while active and new-stack events are blocked.

Availability calculation and the Redis lease are serialized by a short
distributed lock per organization. This prevents a delayed caller from holding
an old availability snapshot until an earlier request has committed usage and
released its lease. Within that decision, Redis admission uses one server-side
atomic operation per microbatch: it purges a bounded number of expired leases,
computes remaining capacity, and records the new lease without scanning active
reservations. Active leases remain organization-scoped across bucket and finite
plan-limit changes until commit, release, or expiry. This can conservatively
delay admission for work crossing a boundary, but it prevents that in-flight
work from disappearing and reusing the same monthly capacity. Unrelated
organizations and replicas continue concurrently; the in-memory implementation
provides the same lease semantics for single-process development and tests.
The leases coordinate V3 admissions. Legacy V2 workers retain their existing
read-then-process quota check during rollout, so a mixed V2/V3 deployment also
retains V2's pre-existing concurrent oversubscription window until that worker
is deliberately migrated to the reservation primitive.

Billable settlement is deliberately at-most-once. The batch writer that gets a
successful result from the create-only event write owns the settlement; a
persisted duplicate only repairs idempotent side effects and is never charged
again. Usage is added to scalar five-minute organization and project counters,
so admission reads and settlement writes remain O(1) and Redis does not retain
one key or set member per event.

An ambiguous Elasticsearch bulk failure cannot prove which racing request
created a document. V3 therefore skips settlement for every ambiguous result
rather than risk charging twice. This narrow crash window can undercount but
cannot overcharge. `ex.ingestion.v3.usage.committed` and
`ex.ingestion.v3.usage.ambiguous_skipped` expose the tradeoff; a future offline
Elasticsearch reconciliation job may repair undercounts without adding a
per-event ledger to the ingestion hot path.

The periodic usage saver acknowledges organization and project discovery ids
individually. It persists the usage document before recording a processed
bucket marker, then removes the scalar counters and finally removes that one
discovery id. A failure leaves the failed id and every unvisited id available
for retry. If persistence and the marker succeeded but cleanup failed, the
retry observes the marker and completes cleanup without applying the bucket a
second time. The last applied bucket is also persisted atomically with the
organization or project totals. A process failure after the repository commit
but before the Redis marker write therefore retries only the Redis
acknowledgement and cleanup; it cannot apply that bucket twice. Legacy documents
without the field apply their first pending bucket normally and establish the
durable marker on that save.

## Decision: side-effect durability

The primary event is authoritative. Notifications, webhooks, archive export,
nonessential geolocation, and repairable derived statistics remain outside the
request critical path.

The inline writer persists deterministic side-effect or repair intent before
acknowledgement. Derived statistics and internal stages are idempotent;
notification and webhook enqueueing uses durable duplicate suppression while
retaining at-least-once failure semantics. Partial bulk outcomes are reconciled
by deterministic identifiers rather than by placing the original event payload
back on the ingestion queue.

The retry-state budget is explicit: statistics and terminal notification
completion share one integer bitmask key per persisted event for the configured
idempotency window. Stack statistics use one project-partitioned atomic Redis
settlement per microbatch: the script claims previously unseen event identities,
updates every affected stack aggregate, and sets the statistics bits together.
Projects hash independently across Redis Cluster slots. Overlapping or
retried batches therefore cannot increment an occurrence twice, and the handler
does not issue a serial command chain per stack. Notification and webhook queue
markers exist only for events that actually trigger those effects. Thus one
million persisted events retain one million expiring side-effect state keys, not
one key per stage. Rollout measurements must include Redis bytes per state key,
script duration, lock commands, and key-expiration churn; a compact sharded-hash
store is the next design step if that measured footprint misses the
cost-per-million target.

## V2 compatibility boundary

V2 retains its routes, payloads, 202 response, controller, and durable queue.
Shared services are extracted below the transport boundary:

- Canonical fingerprinting.
- Stack route resolution.
- Server-side stack parsing.
- Batch stack resolution and creation.
- Batch event persistence.
- Usage accounting primitives.

The V3 request and acknowledgement path must not instantiate V2 queue models,
plugin contexts, or compatibility converters. The asynchronous side-effect
worker may temporarily adapt persisted events to the existing enrichment and
notification actions while those actions are extracted into transport-neutral
services; it does not run the legacy preprocessing or post-processing plugin
pipeline. V2 may adopt a shared service only when its compatibility fixtures
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
- Memory remains bounded by the active-stream guard and microbatch limits during
  long or slow streams. Idle streams do not reserve processing permits.
- A discarded event creates no complete error/frame graph and performs no event
  index write.
- At most one distributed route lookup per unique signature per microbatch.
- The normal V3 path performs no object-storage payload write/read and no
  primary ingestion queue operation.
- Scaling from one to four API instances reaches at least 80% efficiency until
  the shared datastore is the measured bottleneck.
- Rollout evidence must demonstrate lower V3 CPU, allocation rate, and
  time-to-durable-event relative to V2 before those improvements are claimed.
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
- Language-neutral client specification, golden fixtures, black-box conformance
  tests, and timed new-language implementation evidence meeting the client
  effort gates.
- Microbenchmark reports and end-to-end load results for the scenario matrix.
- A staged rollout through the internal project and selected projects with a
  documented rollback switch.

## Implemented API and client behavior

V3 is exposed through `POST /api/v3/events` and
`POST /api/v3/projects/{projectId}/events`. Both are Minimal API routes and
require a client-scoped bearer token. The request content type is
`application/x-ndjson`; each nonblank line contains exactly one complete event
object. Lines may span arbitrary network writes, CRLF is accepted, and the final
newline is optional. Adjacent objects on one line and top-level arrays are
rejected. The routes accept identity, gzip, and Brotli content encoding.

Clients must generate one stable `id` per captured event and reuse it whenever
the containing segment is replayed. On first receipt, the server atomically
derives a project-scoped, date-routable persisted identifier from `id` and the
original event `date`. This path is stateless, so clients must retain both fields
unchanged for retries. When `date` is omitted, a bounded distributed mapping
preserves the first receipt time for the configured idempotency window,
including retries that cross a UTC day boundary. `reference_id` remains a
separate business identifier and is not transport idempotency.

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
explicit manual stacking. Manual signature data is canonicalized by ordinal key
and hashes an unambiguous encoding of both keys and values, making grouping
independent of JSON property order across client languages.

Only `id` and `type` are required. Maintained SDKs should also identify
themselves with `client.name` and `client.version`; applications can send
first-class `version` and `level` values without knowing internal event data
keys. Unknown event properties are ignored so additions remain compatible.
Top-level custom `data` keys beginning with `@`, plus legacy `sessionend` and
`haserror` keys, are reserved and rejected. This prevents custom JSON from
bypassing the typed request redaction and project PII policy or overriding
first-class grouping and metadata. Runtime validation uses the same durable
limits advertised by OpenAPI: 2,000-character messages, 100-character tags,
1,000-character manual stack titles, and reference identifiers containing 8-100
letters, digits, or hyphens.
V3 initially accepts error, log, usage, and custom types. It explicitly rejects
the legacy-only 404 and session lifecycle types until their stateful V2
preprocessing has a native V3 design, avoiding silent semantic drift.

`/api/v3/events` remains an event-only NDJSON stream. It will not acquire a
per-line kind/payload wrapper or a stream header. A future optional,
length-delimited advanced transport can carry the exact same event JSON beside
attachments, minidumps, profiles, or other typed items. This keeps the minimum
client smaller than Sentry's envelope implementation without forcing binary or
heterogeneous data into the event object.

The executable reference client and repeatable load harness live in
`benchmarks/Exceptionless.Ingestion.Load`; usage is documented in
`benchmarks/README.md`. It submits the same conceptual corpus through the real
V2 and V3 endpoints and records four explicitly named boundaries: submission
response, observed full processing, V3 durable acknowledgement, and query
visibility of persisted survivors. Observed full processing follows V2 through
its legacy pipeline, correlated child retries, and final queue/archive cleanup.
For V3 it waits for the existing final `notifications` marker, which is recorded
only after archive, enrichment, statistics, and notification/webhook enqueueing
have succeeded. Query visibility remains a second common downstream comparison
for persisted survivors.

V2 tracking uses per-post correlation rather than global queue depth or sentinel
events, so mixed and 100%-discard runs are measurable. The controller returns
the existing V2 queue entry id without synchronous tracking-cache writes; the
worker lazily creates and updates the opt-in status while processing. V3 status
resolves persisted ids from the existing distributed idempotency mapping and
only reads markers that normal side-effect idempotency already writes, adding no
ingestion write or extending the mapping lifetime. These read-only status
contracts are benchmark-oriented and are disabled unless
`EventIngestionV3:EnableProcessingStatus` is explicitly enabled; this prevents
untrusted V2 clients from creating tracking keys in normal deployments. V3
markers expire with the configured idempotency window. The status routes are
also excluded from public OpenAPI documents so generated SDKs do not treat
benchmark instrumentation as product API. Warmup also waits for full processing
so its side-effect backlog cannot contaminate a measured trial. Both protocols
use the same read-count query. Status polling is bounded, completed chunks are
not polled again, and each trial records the exact observer load. The observer
cardinality is nevertheless different—V2 has one status identifier per request
and V3 has one per persisted event—so observed full-processing time is a
conservative terminal check, not an unqualified measure of pipeline CPU or cost.
Submission and query visibility have the closest apples-to-apples observers;
efficiency claims additionally require correlated server, Redis, and
Elasticsearch telemetry.
The harness supports one-event requests and large client batches, isolates hot
and new stack identities by protocol, alternates protocol order, reports median
multi-trial results, and can emit a secret-free JSON evidence artifact.
Request examples live in `tests/http/events-v3.http`, and the isolated OpenAPI
document is available at `/docs/v3/openapi.json`.

## Configuration and rollout

`EventIngestionV3:Enabled` defaults to `false`. Rollout can be constrained with
`AllowedProjectIds` and `AllowedOrganizationIds`; empty sets allow every
authenticated project. Other controls are:

- `MicroBatchSize`, `MaximumMicroBatchBytes`, and `MaximumEventSize`.
- `MaximumCompressedBodySize`, `MaximumDecompressedBodySize`, and
  `MaximumEventsPerRequest`.
- `MaximumActiveStreams` and `MaximumActiveStreamsPerOrganization` bound open
  streaming requests. Their defaults are the greater of eight times the
  corresponding processing limit or 128 globally and 32 per organization.
  `ActiveStreamQueueLimit` and `ActiveStreamQueueLimitPerOrganization` are
  configurable and default to zero so excess slow connections fail promptly.
- `MaximumConcurrentRequests` and `ConcurrencyQueueLimit` bound simultaneous
  microbatch processor calls globally. `MaximumConcurrentRequestsPerOrganization`
  and `ConcurrencyQueueLimitPerOrganization` provide fair organization admission.
  These processing permits are acquired only while a full microbatch executes.
- `MaximumStackCreationConcurrency`.
- `RequestTimeout` and `IdempotencyWindow`.
- `EnableProcessingStatus` for opt-in benchmark completion endpoints and V2 tracking.
- `StackRouteCacheDuration` and `NegativeStackRouteCacheDuration`.

All stream and processing concurrency limits are per API replica, not cluster
wide. Scaling out therefore adds API-side processing capacity with each replica
while retaining bounded request and queued work on every node; Redis,
Elasticsearch, and downstream workers remain shared bottlenecks that must be
verified with the multi-instance performance gate above.

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
request/environment/geolocation enrichment, notification and webhook enqueueing,
and deterministic archive export when archival is enabled. Work-item,
notification, and webhook identities are stable for the configured idempotency
window. Stack-statistics completion and aggregation are one atomic operation;
there is no interval in which the count has changed but its event identity is
still unclaimed. Other explicit idempotency stages acquire distributed
per-event locks, recheck completion after acquiring them, run the pending
effects, and record completion only after the stage succeeds. A failed or
abandoned worker cannot poison a retry with a premature claim. Externally
visible notification delivery remains at least once and its handlers must be
idempotent. V3 deliberately does not run the legacy post-processing plugin
pipeline: those plugins were written against V2 semantics and are not a safe
compatibility boundary for the new materializer. Retries repair pending work
from the authoritative event documents.
