# Event ingestion V3 client protocol and ecosystem plan

This document defines the client-authoring contract for event ingestion V3. It
also records the Sentry and Raygun research that shaped the protocol. The
primary design metric is not only server throughput: a competent developer in a
new language must be able to build a correct basic sender without copying an
existing Exceptionless SDK or implementing an observability framework.

## Success criteria

A basic client must need only standard platform facilities for HTTPS, JSON,
UTF-8, time, and random identifiers. It must not need to:

- Parse a stack trace into frames.
- Construct an error/inner-error object graph.
- Implement an envelope or multipart grammar.
- Buffer a JSON array before sending.
- Download project configuration before its first event.
- Implement tracing, scopes, breadcrumbs, sampling, symbolication, offline
  storage, or framework integrations before it can report an error.
- Know Exceptionless persistence data keys such as `@error` or `@environment`.

The minimum useful error event is therefore:

```json
{"id":"018f5f5e-8f6d-7a30-bf5b-9a10b0c4d6e7","type":"error","message":"checkout failed","exception_type":"PaymentError","stack_trace":"PaymentError: checkout failed\n    at charge (/app/payments.js:42:7)"}
```

The client writes a newline and may immediately write the next event. The
server owns stack parsing, canonical grouping, discarded-stack detection,
quota admission, and persistence.

Client effort is a release gate. Before the public contract is frozen, run a
timed implementation exercise with developers who have not worked on V3:

- First accepted log from the written specification in 30 minutes or less.
- First accepted handled error with the native raw stack in 60 minutes or less.
- A Profile 0 reference sender in 200 non-generated source lines or fewer in a
  language with a standard HTTP and JSON library, excluding tests and examples.
- No third-party runtime dependency required for Profile 0.
- No Exceptionless-specific concept beyond endpoint, token, event, segment, and
  terminal result required for Profile 0.
- The same event serializer is reused unchanged when reliable buffering or the
  future advanced transport is added.

Failure of one of these gates is a protocol usability defect, not merely a
documentation issue.

## Minimum implementation surface

A dependency-free client has five mandatory responsibilities:

1. Accept a server URL and project client token.
2. Generate a stable event `id` and preserve it for retries.
3. Serialize an object containing `id` and `type`; error clients add the raw
   `stack_trace` and normally `message` and `exception_type`.
4. POST UTF-8 JSON values separated by newlines with
   `Content-Type: application/x-ndjson` and `Authorization: Bearer <token>`.
5. Close the bounded request segment, read the terminal response, and replay
   the same ids after a timeout, connection failure, or retryable HTTP status.

No batching abstraction is required. A first implementation may open a request
for one event at a time. A production client can reuse a connection and keep a
request segment open while events arrive, closing it on the first configured
count, byte, age, flush, or shutdown limit.

The API defaults omitted `date` to server receipt time. This keeps the first
sender small, although production clients should send the capture time. The API
does not require `client`, `version`, `level`, request, environment, user, or
custom data.

## Capability profiles

The ecosystem should describe clients by capabilities instead of treating a
large official SDK as the minimum viable implementation.

### Profile 0: core sender

- Required fields, raw stack traces, token authentication, and NDJSON.
- One-event requests are valid.
- Stable ids and safe whole-segment replay.
- Terminal response handling and bounded event size.

This profile is enough for a community integration to be listed as an
Exceptionless-compatible sender.

### Profile 1: reliable transport

- Connection reuse and bounded streaming segments.
- Count, byte, age, explicit flush, and shutdown segment boundaries.
- Bounded in-memory queue with a documented overflow policy.
- Exponential backoff with jitter and `Retry-After` support.
- Optional gzip or Brotli request compression.
- Optional bounded disk persistence for unacknowledged segments.
- Flush with a caller-provided deadline.

Reliability is deliberately independent from event construction so the same
transport can be reused by framework-specific packages.

### Profile 2: platform SDK

- Unhandled exception integration appropriate to the platform.
- First-class application `version`, severity `level`, and SDK `client`
  metadata.
- Safe request, user, and environment capture with PII controls.
- Optional breadcrumbs, trace correlation, source context, and platform-aware
  diagnostics as those contracts become available.
- A before-send hook that can mutate or discard an event.

### Profile 3: framework integrations

- Thin adapters for web frameworks, logging systems, job processors, desktop
  UI frameworks, and mobile lifecycle APIs.
- Framework packages depend on the platform SDK; they do not reimplement the
  wire protocol or retry queue.

## First-class event fields

Only `id` and `type` are required. Optional fields are additive and a server
must ignore unknown fields so an upgraded client can continue sending to an
older V3 server.

| Concern | V3 field | Client work |
| --- | --- | --- |
| Idempotency | `id` | Generate once and retain through retries. |
| Classification | `type` | Use a known type or a stable custom type. |
| Capture time | `date` | RFC 3339 timestamp; server time is the fallback. |
| Display | `source`, `message`, `value`, `tags` | Optional scalars and string tags. |
| Application release | `version` | Optional release/build version. |
| Severity | `level` | Optional platform severity name. |
| SDK identity | `client.name`, `client.version` | Recommended for maintained SDKs. |
| Error | `exception_type`, `stack_trace` | Send the runtime's original text. |
| Grouping override | `stacking` | Advanced opt-in only. |
| Context | `user`, `request`, `environment` | Optional typed objects. |
| Custom extension | `data` | Arbitrary JSON object within documented limits. |

`client.name` should be stable and ecosystem-wide, for example
`exceptionless.go`, and `client.version` should be the SDK package version, not
the application version. The server maps these fields to submission-client
metadata. This is necessary for compatibility analysis, rollout targeting, and
support without requiring every language to emulate a historical User-Agent.

`data` is an escape hatch for user data and experiments. New Exceptionless
features must not permanently hide standardized semantics in magic `data` keys.
Frequently used, searchable, billable, grouping-sensitive, or security-sensitive
semantics graduate to documented optional fields.

## Comparison with Sentry

Sentry's current event transport packs event JSON into an Envelope sent to
`/api/{project_id}/envelope/`. An envelope contains a JSON header, then one or
more items, each with another JSON header and a payload. Payloads containing
newlines require byte lengths. The protocol can combine events with binary
attachments, batch selected item types, persist envelopes offline, and carry
new item types. Implementations must handle unknown item types and attributes.
See Sentry's official [Envelope specification](https://develop.sentry.dev/sdk/foundations/transport/envelopes/).

Sentry's event itself is a broad, evolving schema. Exceptions are a list of
structured values with type/value, mechanism, thread association, and a
structured stack trace; chained exceptions have defined ordering and tree
metadata. See the official [event payload](https://develop.sentry.dev/sdk/foundations/transport/event-payloads/),
[exception](https://develop.sentry.dev/sdk/foundations/transport/event-payloads/exception/),
and [stack trace](https://develop.sentry.dev/sdk/foundations/transport/event-payloads/stacktrace/)
specifications.

That design gives Sentry a strong heterogeneous transport, but a new SDK has
substantially more surface area:

- Parse a DSN into endpoint, project id, and authentication values.
- Generate Sentry-specific ids and event metadata.
- Turn runtime exceptions and causes into the structured exception model.
- Turn runtime stacks into correctly ordered structured frames.
- Serialize envelope and item headers and compute payload byte lengths.
- Categorize item types for rate limits and process Sentry rate-limit headers.
- Queue envelopes, flush a background worker, apply backpressure, report client
  drops, and optionally persist offline.
- Add platform integrations, scopes, breadcrumbs, request data, release data,
  in-app frames, source context, and tracing for full SDK parity.

This is visible in Sentry's official [SDK expected-features list](https://develop.sentry.dev/sdk/expected-features/)
and in representative open-source transports such as
[sentry-go](https://github.com/getsentry/sentry-go). The comparison is not that
these features are undesirable. It is that Sentry's definition of a complete
SDK couples a much larger product surface to the client-authoring task.

Exceptionless V3 is easier for the first event because it has a normal URL and
bearer token, a two-field minimum object, native NDJSON framing, and server-side
stack parsing. Sentry is more future-proof for binary and non-event telemetry
because its envelope has an explicit item-type and length boundary.

## Comparison with Raygun

Raygun Crash Reporting uses a conceptually simple HTTPS transport:
`POST https://api.raygun.com/entries`, JSON content, and an `X-ApiKey` header.
However, its documented payload requires `occurredOn`, `details`, an error, and
a stack trace with a line number. The error contains structured stack frames,
and the complete details shape includes client, environment, request, response,
user, tags, custom data, breadcrumbs, version, and grouping data. The maximum
documented single payload is 128 KB. See Raygun's official
[Crash Reporting API](https://raygun.com/documentation/product-guides/crash-reporting/api/).

The open-source clients show where that work lands. The
[Node client](https://github.com/MindscapeHQ/raygun4node) depends on a stack
parser, converts call sites into line/column/class/file/method frames, recursively
builds inner errors, gathers environment and request context, filters circular
custom data, and then serializes the nested message. Its optional batch
transport joins fully serialized messages into a large JSON array for
`/entries/bulk`. The [Android client](https://github.com/MindscapeHQ/raygun4android)
adds uncaught exception handling, offline caching, device identity, network
state, and obfuscation mapping concerns. The
[.NET client](https://github.com/MindscapeHQ/raygun4net) walks runtime stack
frames and portable executable debug information before submission.

Raygun is easier than Sentry at the HTTP framing layer, but harder than
Exceptionless V3 for a new error sender because the client must understand and
materialize Raygun's error graph. Raygun's crash schema has evolved through
optional fields and custom data, while other products and browser report types
use separate endpoints. That separation avoids destabilizing crash ingestion,
but it does not provide Sentry's general multi-item transport.

## Apples-to-apples implementation comparison

The comparison below is for a new language's first correct handled-error
sender, not for installing an existing SDK and not for full automatic
instrumentation.

| Responsibility | Exceptionless V3 | Raygun Crash API | Sentry Envelope API |
| --- | --- | --- | --- |
| Endpoint configuration | URL plus token | Fixed/default URL plus API key | Parse DSN and derive endpoint/authentication |
| Minimum event shape | `id`, `type` | `occurredOn`, `details`, error, stack frame line | Envelope header, item header, event id, event JSON |
| Stack input | Original string | Structured frames | Structured frames |
| Chained errors | Server parses common raw forms | Client builds inner-error graph | Client builds ordered exception values/tree |
| Multiple events | Stream independent JSON values | Single documents or buffered JSON array bulk | Multiple envelope items only where item rules allow |
| Binary attachment | Future advanced transport | Not part of crash JSON contract | Native length-delimited attachment item |
| Unknown future fields | Ignored; additive | Optional/custom data | Preserved/ignored according to envelope scope |
| Minimum retry identity | Required stable event id | No equivalent id in documented crash body | Event id |
| Minimum dependencies | Standard HTTPS/JSON | HTTPS/JSON plus stack inspection/parser | HTTPS/JSON plus stack model and envelope framing |

For a production-grade SDK, all three eventually need bounded buffering,
timeouts, retries, redaction, unhandled-error hooks, and platform integrations.
Exceptionless should publish those as higher profiles instead of blocking the
first client on them.

## Decision: keep the event endpoint permanently simple

`POST /api/v3/events` remains a homogeneous stream of event objects. We will
not add a wrapper such as `{ "kind": "event", "payload": ... }` to every line,
and the first line will not become a stream header. Those designs tax every
event and every client for features most clients never use.

The event object evolves additively:

- Existing field meanings do not change.
- New fields are optional.
- Unknown fields are ignored.
- Fields may be promoted from experimental `data` conventions to first-class
  fields, with a server transition period that accepts both.
- A semantic change that cannot follow these rules requires a new endpoint or
  media type, not a `schema_version` switch inside every event.
- Limits and validation errors are documented and machine-testable.

This gives ordinary event clients the smallest contract while allowing the
event model to grow with optional release, severity, SDK, context, breadcrumb,
trace, thread, module, and advanced error data.

## Decision: reserve an advanced heterogeneous transport

Attachments, minidumps, profiles, replays, symbol files, and other binary or
non-event records must not be base64 fields added to the event object. A future
advanced endpoint will use explicit item headers containing at least item type,
content type, and byte length. Unknown item types must be safely skippable.

The advanced transport must embed the exact V3 event JSON as its `event` item
payload. A client upgrades by adding framing around the serializer it already
has; it does not maintain a second event model. The endpoint can then support
atomic event-plus-attachment submissions and independent limits or billing
categories without changing `/api/v3/events`.

The advanced transport is intentionally not required for Profiles 0 through 2
until a client needs a feature that cannot be represented as an event. Its
design should reuse the proven parts of Sentry's envelope—typed items,
byte-length framing, unknown-item handling, and common metadata—without copying
Sentry-specific DSNs, authentication, event schema, or SDK feature requirements.

## Reserved evolution paths

The following additions must remain possible without breaking the simple
error path:

- Advanced exceptions: an optional structured exception collection for
  runtimes that can supply cause trees, mechanisms, handled state, native
  codes, or pre-parsed frames. `exception_type` and `stack_trace` remain the
  preferred simple form; precedence and mutual-exclusion rules must be explicit.
- Breadcrumbs: a bounded optional collection with timestamp, category, level,
  message, and JSON data.
- Trace correlation: optional W3C-compatible trace and span identifiers. Full
  transaction/span ingestion may use its own item type.
- Threads and modules: optional diagnostic collections, never required to send
  an error.
- Deployment identity: additive environment/release/distribution fields that
  do not conflict with the existing machine `environment` object.
- Key/value attributes: an indexed bounded scalar map if string `tags` and
  arbitrary `data` prove insufficient.
- Client outcomes: an optional aggregate item for locally discarded events,
  queue overflow, serialization failure, and rate limiting.

Names for these fields are reserved in the public specification before SDKs
invent incompatible custom-data conventions. They are implemented only with a
server consumer and cross-language fixtures.

## Client conformance kit

The public launch is not complete with OpenAPI alone. NDJSON streaming and
retry semantics need executable, language-neutral evidence:

1. Publish a short normative wire specification with exact request, response,
   status, retry, size, compression, and field-precedence rules.
2. Publish JSON Schema for one event and keep the isolated V3 OpenAPI document.
   Explain that OpenAPI's single request schema represents each streamed value,
   not an array.
3. Publish golden request and response fixtures for minimal log, minimal error,
   Unicode, multiline stacks, multiple values, compression, partial invalid
   results, duplicates, discarded events, and unknown future fields.
4. Ship a black-box conformance runner. It starts a local receiver, invokes a
   client adapter, deliberately drops a response, splits JSON across writes,
   returns 429/503, and verifies stable-id replay and bounded flush behavior.
5. Ship a tiny reference transport in pseudocode plus dependency-free examples
   in at least JavaScript, Python, Go, Java/Kotlin, Rust, and Swift. Examples
   demonstrate the core sender, not a framework-sized official SDK.
6. Give ecosystem packages a machine-readable capability manifest and display
   Profile 0/1/2/3 badges in the client directory.
7. Run every maintained client against the same fixtures in CI. A contract
   addition lands only after old-client, new-client, and unknown-field cases
   pass.

## Client ecosystem implementation sequence

1. Freeze the Profile 0 field semantics, status/retry matrix, limits, and
   additive-evolution rules. Keep the server's unknown-field test and isolated
   OpenAPI baseline as compatibility gates.
2. Extract the current load generator's V3 writer into a small reference
   transport whose public surface is event serialization, segment policy,
   send, and flush. Do not expose server persistence models.
3. Publish JSON Schema, golden fixtures, and the black-box conformance runner.
   Make the runner usable before an SDK repository or package is accepted.
4. Build two pilot clients in unrelated ecosystems, recommended Python and Go,
   from the specification rather than by porting the .NET client. Record time,
   source lines, dependencies, ambiguities, allocations, and retry defects.
5. Resolve every cross-language ambiguity in the normative specification, then
   repeat the timed exercise with Java/Kotlin and Swift or Rust. Do not freeze
   the contract based only on .NET and JavaScript ergonomics.
6. Publish the Profile 0 examples and Profile 1 transport guidance. Create a
   client directory showing owner, package, supported runtime versions,
   capability profile, conformance version, and last successful conformance run.
7. Add official platform integrations only after the shared transport passes
   conformance. Keep framework packages thin so transport fixes flow to the
   whole ecosystem.
8. Design the optional advanced item transport when the first concrete binary
   or heterogeneous feature needs it. Validate that an existing Profile 0 event
   serializer can be embedded unchanged before accepting the design.

## Review gates for future protocol changes

Every proposal that changes ingestion must answer:

- Does a Profile 0 sender need to change? If yes, use a new endpoint/media type.
- Can the server compute or default the value more reliably than every client?
- Is the field needed for grouping, discard classification, billing, search,
  redaction, or display? If so, it should not remain an undocumented data key.
- Can old servers ignore it and can old clients omit it?
- Does it require a binary payload or a different retention/billing category?
  If so, use the advanced item transport.
- What are its byte, count, depth, and privacy limits?
- Are field precedence and duplicate/retry behavior deterministic?
- Do golden fixtures cover at least two unrelated language implementations?

These gates preserve the strongest aspect of V3—the server does expensive,
product-specific interpretation once—while retaining a clean path to richer
telemetry.
