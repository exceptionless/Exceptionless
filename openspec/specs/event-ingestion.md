# Spec: Event Ingestion

Baseline spec for how events enter and are processed by Exceptionless.

> **Implementation-derived values** are included to guide future changes, but they are not public compatibility contracts unless explicitly stated in the relevant requirement.

## Submission Endpoints

- `POST /api/v2/events` — primary event submission (authenticated via `access_token` query param or Bearer token).
- `GET /api/v2/events/submit` — submit via query parameters (tags, message, etc.).
- `POST /api/v2/events/by-ref/{referenceId}/user-description` — attach user description to an existing event by reference ID.

## Accepted Formats

- **JSON body** — single event object or array.
- **Plain text** — newline-delimited simple string events.
- Content-Type: `application/json` or plain text.

## Event Model (public contract)

| Field | Type | Notes |
|-------|------|-------|
| type | string | Known types: `error`, `usage`, `log`, `404`, `session`, `sessionend`, `heartbeat` |
| source | string? | Source identifier (e.g., URL, method) |
| date | DateTimeOffset | Event timestamp |
| tags | string[]? | User-defined tags |
| message | string? | Human-readable message |
| geo | string? | Lat,lng format |
| value | decimal? | Numeric value |
| count | int? | Occurrence count |
| data | object? | Extensible data dictionary; well-known keys prefixed with `@` |
| reference_id | string? | Client-side reference for correlation |
| session_id | string? | Session tracking |

## Well-Known Data Keys (prefixed `@`)

- `@user` — `{ identity }` user info.
- `@user_description` — `{ email_address, description }`.
- `@error` / `@simple_error` — error/exception info.

## Processing Pipeline

Events flow through the `EventPipeline` which executes ordered actions:

1. `005_RunEventProcessingPluginsAction` — plugin pre-processing
2. `006_TruncateFieldsAction` — field length enforcement
3. `030_CheckForRegressionAction` — detect regressions on fixed stacks
4. `035_CopySimpleDataToIdxAction` — copy searchable fields to index
5. `050_MarkProjectConfiguredAction` — mark project as having received data
6. `060_UpdateStatsAction` — update stack/project statistics
7. `090_IncrementCountersAction` — metrics counters
8. `100_RunEventProcessedPluginsAction` — plugin post-processing

## Queue Flow

1. Event submission → serialized as `EventPost` → enqueued to `IQueue<EventPost>`.
2. `EventPostsJob` dequeues → deserializes → runs through `EventPipeline`.
3. Pipeline outputs trigger downstream queues: `EventNotification`, `WebHookNotification`.

## Usage Enforcement

- Events are counted against organization plan limits via `EventUsageJob`.
- Over-limit events are rejected or flagged.

## Compatibility Boundaries

- The event JSON schema (field names, types, data key conventions) is a public SDK contract.
- `access_token` query parameter authentication for submission must remain supported.
- `reference_id` correlation and `user-description` attachment flows are SDK-facing.
- Pipeline action ordering defines behavior semantics (e.g., regression detection before stats).

## Internal Implementation Baselines

### Event post size limits are configuration-driven

`AppOptions.MaximumEventPostSize` controls event post payload size enforcement. The default is 200,000 bytes. This is an internal configuration value; the exact limit is not documented as a public API contract.

#### Scenario: Event post exceeds configured size

Given an event post payload exceeds the configured maximum event post size
When the event post job processes the payload
Then the payload is completed or rejected according to event post job behavior.

### Failed batch event processing may retry individual events

When a batch event post contains events that fail pipeline processing for retryable reasons, the event post job may re-enqueue failed events individually rather than retrying the entire batch. `ArgumentException` and `DocumentNotFoundException` pipeline failures are completed (not retried); single-event failures are abandoned.

**Batch size:** `AppOptions.BulkBatchSize` defaults to 1000. This is an internal processing setting, not a documented public API contract.

### Requirement: Event submission size and batch limits are configuration-driven

Exceptionless event submission processing uses internal configuration for maximum event post size and bulk batch size. These values guide implementation and operations, but they are not treated as documented public API contracts unless client/API documentation explicitly defines them.

#### Scenario: Event post size behavior changes

Given a change modifies maximum event post size handling
When the change is proposed
Then the change must document the configured limit, compressed and uncompressed processing behavior, and whether API/client documentation needs to be updated.

#### Scenario: Bulk batch behavior changes

Given a change modifies bulk event batch processing
When the change is proposed
Then the change must document the configured batch size behavior and whether the limit is an internal processing setting or a public API contract.

**Note:** Maximum event payload size and batch submission count are internal implementation configuration values. They are not documented as public API limits for clients.
