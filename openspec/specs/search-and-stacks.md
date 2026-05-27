# Spec: Search and Stacks

Baseline spec for event search, filtering, aggregations, and stack management.

> **Implementation-derived values** are included to guide future changes, but they are not public compatibility contracts unless explicitly stated in the relevant requirement.

## Stacks

A Stack groups duplicate events by signature hash. Core stack properties:

| Field | Notes |
|-------|-------|
| Id | Unique identifier |
| OrganizationId, ProjectId | Ownership |
| Type | Event type (error, usage, log, 404, session) |
| Status | `Open`, `Fixed`, `Ignored`, `Discarded`, `Regressed`, `Snoozed` |
| SignatureHash | Deduplication key |
| Title | Human-readable summary |
| TotalOccurrences | Aggregate count |
| FirstOccurrence, LastOccurrence | Time range |
| Tags | User-applied tags |
| OccurrencesAreCritical | Critical flag |
| FixedInVersion | Version that resolved the issue |
| SnoozeUntilUtc | Snooze expiration |
| References | External links |

## Stack Status Transitions

- `POST /api/v2/stacks/{id}/change-status?status={status}` — set status directly
- `POST /api/v2/stacks/{id}/mark-fixed` — mark as fixed
- `POST /api/v2/stacks/{id}/mark-snoozed?snoozeUntilUtc={datetime}` — snooze until
- `POST /api/v2/stacks/{id}/mark-critical` / `DELETE .../mark-critical` — toggle critical
- `POST /api/v2/stacks/{id}/promote` — promote to external tracker
- `POST /api/v2/stacks/{id}/add-link` — add external reference link
- Bulk operations: comma-separated IDs in route

## Search & Filtering

- Events and stacks are queried with `filter` query parameter using Lucene-style syntax.
- Filterable fields include: `status`, `type`, `tag`, organization/project scoping.
- Example: `filter=status:open`, `filter=type:error status:open`, `filter=tag:production`.

### Requirement: Search filter syntax follows documented Exceptionless query behavior

Exceptionless search must support the documented filter/search syntax including: searchable fields, multiple query terms, OR expressions, wildcard suffixes, exclusion terms, `_missing_` / `_exists_` field checks, ranges, and indexed simple extended data fields.

#### Scenario: Multiple filters are combined

Given a user enters multiple query terms separated by spaces
When the search is executed
Then the terms are treated as an AND operation unless an explicit OR expression is used.

#### Scenario: Extended data is indexed for simple values

Given an event contains simple extended data values
When the event is indexed
Then string, boolean, date, and number values are searchable using the documented extended-data field syntax.

## Aggregations

- `GET /api/v2/events/count` supports an `aggregations` parameter.
- Aggregation DSL: `date:(date~month+cardinality:stack+sum:count~1)+cardinality:stack+terms:(first+@include:true)+sum:count~1`
- Used for charts, session analytics, and dashboard widgets.

## Scoping

- By organization: `?organization={id}`
- By project: `/api/v2/projects/{id}/events`
- By stack: `/api/v2/stacks/{id}/events`

## Sessions

- Session events (`type:session`) have dedicated endpoints.
- `GET /api/v2/events/sessions/{sessionId}` — events in a session.
- `GET /api/v2/organizations/{id}/events/sessions` — session list with summary mode.

## Elasticsearch Backing

- All search, filtering, and aggregation is backed by Elasticsearch.
- Indexes are managed via Foundatio.Repositories; direct `IElasticClient` use is prohibited.
- `MaintainIndexes` job handles index lifecycle.

**Implementation-derived:** `AppOptions.MaximumRetentionDays` defaults to 180 days. The FAQ documents project-level data reset/delete behavior. The exact cleanup schedule is enforced by `CleanupDataJob`. These are internal configuration values, not public API promises.

## Compatibility Boundaries

- Filter syntax (Lucene-style) is an SDK/UI contract.
- Aggregation DSL format is used by frontend dashboards.
- Stack status values and transition endpoints are public API.
- `mode=summary` query parameter behavior must be preserved.

### Requirement: Event sorting defaults to descending event date

Event query endpoints default to descending event date sorting when no explicit sort is provided.

#### Scenario: Event query omits sort

Given an event query request does not provide a sort parameter
When the query is executed
Then events are sorted by descending event date.

### Requirement: Stack rollup modes do not support explicit event sort

When event queries are executed in stack rollup modes, explicit sort parameters are not supported.

#### Scenario: Stack rollup query includes sort

Given an event query is executed in a stack rollup mode
When the request includes an explicit sort parameter
Then the API rejects the request rather than silently applying unsupported sorting.

## Sort Behavior

**Event sort fields (from `EventIndex.Alias`):** Any indexed field alias is accepted by the sort expression. Documented aliases include: `date`, `type`, `source`, `message`, `tag`, `geo`, `value`, `count`, `first`, `version`, `level`, `submission`, `ip`, `useragent`, `path`, `reference`, `stack`, `organization`, `project`, `id`, and extended data fields via the `idx` namespace. Prefix with `-` for descending order (e.g., `-date`).

**Invalid sort in stack rollup mode:** `400 Bad Request` with message `"Sort is not supported in stack mode."` (explicitly enforced by the controller).

**General invalid sort fields:** Not validated by the controller. The sort expression is passed directly to Foundatio.Repositories/Elasticsearch; unknown fields result in a runtime error.

**Stack endpoint sort:** Stack query endpoints accept a `sort` query string passed as a sort expression directly to Foundatio.Repositories. Any indexed stack field alias is accepted (`status`, `type`, `first`, `last`, `title`, `tag`, `occurrences`, `critical`, `fixed`, `fixedon`, `version_fixed`, `signature`, etc.).
