# Event ingestion V3 architecture

This document records the design of the V3 event ingestion endpoint for
[issue #2368](https://github.com/exceptionless/Exceptionless/issues/2368). It is
separate from the public API documentation until the V3 contract is ready for
rollout.

The companion [client protocol and ecosystem plan](/event-ingestion-v3-client-protocol/)
defines the wire contract, the minimum sender, capability profiles, and the
Sentry and Raygun comparison.

## Objectives

- One endpoint that is trivial for a single event and efficient for a large
  stream of events.
- No request-sized buffers: events are read from the request body as they
  arrive and processed in bounded microbatches.
- Inline acknowledgement: a successful response means every event reached a
  terminal outcome, so clients need no polling or second request.
- Identical processing to V2. The same event sent to either version is stacked,
  enriched, stored, counted, and notified the same way.
- Correct when scaled out, with no sticky sessions or process-local state.

## Decision: V3 is a transport over the existing event pipeline

The first V3 prototype implemented a second ingestion pipeline: its own stack
fingerprinting, server-side stack parsing, stack route cache, quota
reservations, batch writer, statistics pipeline, and notification worker. It
grouped errors differently from V2, so moving a project from V2 to V3 would have
duplicated every stack and lost stack status, history, and notification state.
It also missed V2 behavior such as source map symbolication, bot throttling,
sessions, and 404 handling, and it changed shared V2 code.

V3 now reuses the pipeline that processes V2 event posts. For each microbatch,
the endpoint maps V3 events to `PersistentEvent` instances and calls
`EventPipeline.RunAsync`, the same call `EventPostsJob` makes for a V2 post.
Every pipeline feature, plugin, and project setting therefore applies to V3
without a second implementation, and future pipeline work benefits both
versions.

Compared to V2, V3 removes these steps from the path of every event:

1. Writing the request payload to file storage.
2. Enqueuing a pointer and waiting for a worker to dequeue it.
3. Downloading the payload and decompressing it into a second byte array.
4. Parsing the complete payload into one collection.

## Request handling

`POST /api/v3/events` uses the project of the client API key, and
`POST /api/v3/projects/{projectId}/events` targets an explicit project. Both
require a client token (`Authorization: Bearer <token>`).

The request body can be:

- A single JSON event object, pretty-printed or compact.
- A JSON array of event objects. Elements are read one at a time; the array is
  never buffered.
- Event objects separated by whitespace, such as newline-delimited JSON.

`EventIngestionV3StreamReader` finds event boundaries by scanning JSON tokens
from the request `PipeReader`, carrying its scanner state across reads so a
fragmented event is never rescanned. An event's bytes stay in the pipe until the
event is complete and are deserialized in place. Because the token scanner cannot
consume part of a token, a long incomplete token is rescanned only after the
unscanned bytes double, which keeps the work linear. Each event is limited to
`MaximumEventSize`; a larger event is skipped by a byte scanner that tracks
strings and nesting, consumes every byte so nothing is retained, and reports the
event as `event_too_large`.

A problem with one event does not fail the request:

- An event that is not a JSON object, or that does not match the event format,
  is reported as invalid and skipped.
- Invalid JSON outside an array is reported and skipped by resuming at the next
  line that begins with `{` (in the first column, so nested objects in a broken
  pretty-printed event are not mistaken for events).
- Invalid JSON inside an array cannot be resynchronized reliably, so it ends the
  request with `400` and a partial result.

Accepted content types are `application/json`, `application/x-ndjson` and its
common aliases, any `application/*+json` type, or no content type. The body must
be UTF-8. gzip and Brotli request compression are supported with independent
compressed and decompressed size limits.

## Processing

Events are grouped into microbatches of up to `MicroBatchSize` events and
`MaximumMicroBatchBytes` bytes. For each microbatch the processor:

1. Maps each V3 event to a `PersistentEvent`. Custom `data` is normalized the
   same way V2 normalizes deserialized event data, then first-class properties
   are stored under the data keys V2 clients use (`@error`, `@simple_error`,
   `@request`, `@environment`, `@user`, `@stack`, `@version`, `@level`) and the
   type is defaulted exactly as V2 does.
2. Claims the `id` of every event that has one. An event whose id is already
   stored is acknowledged as a duplicate; one whose id is still being processed
   by another request is reported as in progress.
3. Applies the organization event limit with `UsageService.GetEventsLeftAsync`,
   as `EventPostsJob` does, and counts the remainder as blocked.
4. Runs `EventPipeline.RunAsync` with an `EventPostInfo` that carries the
   client user agent, IP address, and API version 3.
5. Records usage with the same `UsageService` calls as `EventPostsJob`.
6. Marks the ids of stored and discarded events as stored, and releases the ids
   of events that were blocked, invalid, or failed, so a resend is processed
   again.

Each pipeline outcome maps to one response count: processed events are
`persisted`, cancelled events (for example a discarded stack or an event older
than the retention period) are `discarded`, validation failures are `invalid`,
and other errors are `failed`.

Reading pauses while a microbatch is processed, so HTTP flow control applies
backpressure to fast producers. Overlapping reading and processing is a possible
later optimization if measurements show a benefit.

### Idempotency

An event `id` is optional. When present, the processor hashes it into a
project-scoped cache key and claims it as pending with an atomic add. The claim
becomes stored for `IdempotencyWindow` (one day by default) once the event is
stored or discarded, and is released if the event is not processed. A pending
claim expires shortly after the request timeout, so an instance that stops
mid-request cannot block the id for long.

A resend whose id is stored is acknowledged as a duplicate. A resend whose id is
still pending, because the original request is still running, is reported as
`event_in_progress` and the request returns `503` with `Retry-After`. Reporting
it as a duplicate would lose the event if the original request then failed.
This costs one cache add per event with an id, one batched write per microbatch,
and a read only when an id was already claimed. Events without an id cost
nothing.

Unlike V2, V3 does not copy `id` into `reference_id`: the id is only for
duplicate detection, and `reference_id` remains an application identifier.

### Limits and backpressure

Each API instance applies two independent concurrency limits. An active-stream
limit bounds open requests globally and per organization; it is acquired before
the raw request body limit is relaxed for the V3 endpoint. A processing limit
bounds concurrent microbatches globally and per organization and is held only
while a microbatch is processed, so idle streams do not hold processing
capacity. Full queues return `429` with `Retry-After`.

Organizations that are suspended or out of events receive `402` before the body
is read.

## Responses

A `200` response counts every event in exactly one outcome: `persisted`,
`discarded`, `duplicate`, `blocked`, `invalid`, or `failed` (always zero in a
`200`). Up to 100 errors list the zero-based `index`, the `id` when known, a
stable `code`, and a message.

| Status | Meaning | Client action |
| --- | --- | --- |
| `200` | Every event reached a terminal outcome. | Done. Do not resend. |
| `400` | Malformed JSON array or compressed body. | Fix the request. |
| `401`, `403`, `404` | Authentication, authorization, or project problem, or V3 is not enabled. | Fix the configuration. |
| `402` | The organization is suspended or out of events. | Stop until the plan changes. |
| `413` | The compressed or decompressed body is too large. | Send smaller requests. |
| `415` | Unsupported content type, charset, or encoding. | Fix the request. |
| `422` | Every event was invalid. | Fix the events. |
| `429` | Too many open streams or busy processing. | Resend after `Retry-After`. |
| `503` | Submission is disabled, storage failed, or some events failed. | Resend after `Retry-After`. |

When a request fails after some events were processed, the problem response
includes `partial_result` with the counts so far and `retry_guidance` that
depends on whether the status is retryable.

## Configuration and rollout

`EventIngestionV3:Enabled` defaults to `false`. Rollout can be constrained with
`AllowedProjectIds` and `AllowedOrganizationIds`; empty sets allow every
authenticated project. Other settings:

- `MicroBatchSize`, `MaximumMicroBatchBytes`, and `MaximumEventSize`.
- `MaximumCompressedBodySize` and `MaximumDecompressedBodySize`.
- `RequestTimeout` and `IdempotencyWindow`.
- `MaximumActiveStreams`, `ActiveStreamQueueLimit`,
  `MaximumActiveStreamsPerOrganization`, and
  `ActiveStreamQueueLimitPerOrganization`.
- `MaximumConcurrentRequests`, `ConcurrencyQueueLimit`,
  `MaximumConcurrentRequestsPerOrganization`, and
  `ConcurrencyQueueLimitPerOrganization`.

All concurrency limits are per API instance. Elasticsearch, Redis, and the
background jobs that send notifications remain shared resources.

Start with the internal Exceptionless project in the allowlist, compare stack
assignment, response counts, usage, and resource use against V2, then widen the
allowlists. Rollback is immediate: disable `EventIngestionV3:Enabled` and send
events to the unchanged V2 endpoint.

## Follow-up work

These improvements are deliberately outside this change. Each one belongs in
the shared pipeline so V2 benefits too.

- **Server-side stack trace parsing.** V3 stores `exception_type` and
  `stack_trace` as a V2 simple error, which groups by the exact trace like V2
  simple errors do. Parsing raw traces into structured errors should replace the
  existing `SimpleErrorPlugin` TODO, verified with golden fixtures from real SDK
  output so V2 and V3 produce identical signatures. The earlier prototype parser
  is preserved in commit `17bb0a30d`.
- **Discarded stacks before quota.** Identify events for discarded stacks before
  the organization event limit is applied, so they are never blocked.
- **Per-item bulk results.** Create-only bulk writes with per-item results would
  give exact persisted and duplicate outcomes for concurrent resends without a
  separate idempotency key.
- **Overlapped reading.** Read the next microbatch while the current one is
  processed, if measurements show a benefit.

## Measurement

Compare V2 and V3 on the same commit, machine, Elasticsearch and Redis topology,
payload corpus, compression setting, and client concurrency. Record the commit,
runtime versions, processor topology, datastore versions and resources, instance
counts, payload size distribution, and stack cardinality with every result.

Measure one hot stack, many active stacks, new stacks, discarded stacks, small
and large events, compressed and uncompressed requests, and one, two, four, and
eight API instances. Capture events per second, CPU time and allocations per
event, p50/p95/p99 request latency, time until events are visible to queries,
and datastore operations per event. The load harness in `benchmarks/` submits
the same corpus to both versions; its usage is described in
`benchmarks/README.md`.
