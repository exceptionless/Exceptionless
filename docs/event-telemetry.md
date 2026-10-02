# Event measurements and relationships

Any event type can carry numeric observations, categorical labels, an outcome, a detailed result, and relationships to other events. These are native event fields, independent of `data` and custom-field configuration. A producer can use `type:operation` for tests, benchmarks, jobs, imports, or other repeatable work; no particular type is required.

```json
{
  "type": "operation",
  "source": "Import.Accounts",
  "date": "2026-10-01T12:00:00Z",
  "reference_id": "invocation-1234",
  "parent_reference_id": "suite-run-1234",
  "root_reference_id": "build-run-1234",
  "outcome": "success",
  "result": "completed",
  "labels": {
    "branch": "main",
    "runtime": "net10",
    "scenario": "10000-records"
  },
  "measurements": [
    { "name": "duration", "value": 12.4, "unit": "s" },
    { "name": "allocated", "value": 48192, "unit": "By" },
    { "name": "sql.calls", "value": 4, "unit": "{call}" },
    { "name": "redis.calls", "value": 0, "unit": "{call}" }
  ]
}
```

## Field contract

- **Measurements:** at most 32 uniquely named observations per event. Names start with a letter and contain letters, digits, dots, underscores, or hyphens (100 characters maximum). Values are finite doubles, including zero and fractional values. A missing value is invalid; a missing measurement is absent, not zero. Integers above 2^53 cannot be represented exactly. Units are required, case-sensitive strings of at most 32 characters. Follow UCUM/OTel conventions: prefer `s` for duration, `By` for bytes, `1` for dimensionless ratios, and annotations such as `{call}` or `{request}` for counts. Keep units separate from measurement names. Existing units such as `ms` remain valid; ingestion does not validate the full UCUM grammar. Units do not choose the numeric storage type. No automatic unit conversion occurs: select the exact unit when querying or charting.
- **Labels:** at most 32 name/string pairs, using the same name rules and values up to 256 characters. `"4.90"` and `"00123"` remain exact strings. Labels are categorical; numeric comparisons belong in measurements. There is no inference of numbers, dates, or booleans.
- **Outcome:** optional, with the ECS values `success`, `failure`, or `unknown`. Use `unknown` when an operation's outcome is unknown; omit the field when an outcome is not applicable. Values are lowercase and case-sensitive.
- **Result:** an optional detailed result of 1–100 characters, such as `completed`, `skipped`, `cancelled`, `aborted`, or `timed_out`. It is a keyword string, not a prescribed taxonomy. A result does not infer an outcome: a producer can report `outcome:failure` with `result:timed_out`, or just `result:skipped` when no success/failure outcome applies.
- **Relationships:** `parent_reference_id` identifies the immediate parent, and `root_reference_id` identifies the enclosing execution. References use the existing 8–100 character alphanumeric/hyphen contract and are scoped to the same project. The producer supplies these values. Parent events can arrive later or be outside retention; ingestion does not require them to exist. A direct self-parent is invalid. Navigation follows one link at a time and does not recursively expand a graph, so indirect cycles cannot trigger unbounded traversal. Existing `data["@ref:name"]` relationships keep their current meaning.

Keep `source` and the stacking signature stable across executions to obtain the history of a repeatable operation. Give each execution a distinct reference; do not put a run ID in its source or stack signature. Reference IDs retain their existing deduplication semantics; they are not permanent uniqueness guarantees or an exactly-once ingestion API.

Native fields do not change `value`, infer a new event type, or create stack signatures. Older unstructured root properties whose shapes differ from the new fields are retained in `data` by the inbound parser. Noncanonical root outcomes (for example `"outcome":"skipped"`) also remain legacy data; they are not native outcomes. Producers must use the canonical values to opt into outcome semantics. Existing stored `data` entries remain there.

## Stacks and regression

A stack groups repeated occurrences of the same operation across executions. Parent/root references link event instances within one execution. An operation can therefore retain one stack across many builds while each occurrence belongs to a different run hierarchy. Outcome, result, labels, and measurements do not automatically become part of the stack signature, so success and failure observations can share their history.

For a fixed stack, `outcome:failure` is eligible for regression under the existing fixed-date or fixed-version rules. Success, unknown, and result-only events remain visible without regressing the stack or being discarded as old-version failures. Events with neither native outcome nor result keep legacy regression behavior. A successful observation does not automatically resolve an open or regressed stack. This is stack lifecycle behavior; detecting performance regressions or flaky tests still requires follow-up analysis.

## ECS and OpenTelemetry alignment

The native contract adopts useful shared semantics without claiming wire compatibility with ECS or OTLP:

| Native field | Alignment and conversion boundary |
| --- | --- |
| `outcome` | Uses [ECS `event.outcome`](https://www.elastic.co/docs/reference/ecs/ecs-allowed-values-event-outcome). OTel span status needs an adapter: `Error` can indicate failure, but `Unset` must not blindly become unknown because successful spans commonly leave status unset. |
| `labels` | Follows the flat categorical semantics of [ECS labels](https://www.elastic.co/docs/reference/ecs/ecs-base). Exceptionless accepts strings only and indexes the collection as Elasticsearch `flattened`; numeric-looking values remain keyword values. These are not a replacement for OTel's typed attributes. |
| `measurements` | Uses [OTel instrument-unit conventions](https://opentelemetry.io/docs/specs/semconv/general/metrics/#instrument-units). Values are individual numeric observations. Full OTLP metric types, aggregation temporality, int64 precision, and histogram payloads are not represented by this contract. |
| A `duration` measurement | Prefer seconds. [ECS `event.duration`](https://www.elastic.co/docs/reference/ecs/ecs-event) is integer nanoseconds, so an adapter must explicitly convert values and units. The existing event `value` field retains its current semantics. |
| `result` | Exceptionless's generic detailed result; it has no direct ECS `event.result` counterpart. A future test/CI adapter can translate the relevant OTel semantic conventions. |
| `reference_id`, `parent_reference_id`, `root_reference_id` | Identify individual Exceptionless events. They are not `trace.id`, `span.id`, or OTel parent span identifiers. A trace can contain many spans, and multiple events can refer to the same span. A root event reference is not a trace ID. |

First-class trace context, typed attributes, resource identity, and ECS/OTLP ingestion adapters are follow-up work. They should have native contracts rather than rely on `data` or configured custom fields. Keep the distinction between a resource (the entity producing telemetry) and attributes of an individual observation. Existing Exceptionless event types also retain their meaning; ECS event categorization is a different taxonomy.

## Queries and charts

Use existing event search and sort APIs:

```text
type:operation outcome:failure
result:timed_out
labels.branch:main
measurement.duration@ms:>500
project:PROJECT_ID parent_reference_id:suite-run-1234
project:PROJECT_ID (reference:build-run-1234 OR root_reference_id:build-run-1234)
```

`sort=-measurement.duration@ms` orders individual events by a specific measurement and unit. The `measurement.NAME@UNIT` search alias binds the name, unit, and numeric value to the same nested observation. Normal numeric aggregation expressions can use this alias too. Use simple unit symbols in search aliases; the structured chart API accepts the full unit string.

The events dashboard's **Configure chart** control selects a measurement/unit, aggregation (average, minimum, maximum, sum, count, p50, p95, p99), line/bar display, and split by source, stack, outcome, result, or a named label. With no measurement, bucketed counts can be split by outcome or result. Counts represent stored event observations, not measurement sums or the legacy duplicate `count` field.

Charts inherit the view's filter and time range. They can show UTC time buckets or individual event observations. Bucketed measurements omit missing values and display gaps. Individual observations retain event IDs for drilldown. Bucket drilldown selects the series, measurement, unit, and interval. The expandable observations list provides keyboard access and separates events with identical timestamps.

Bucket charts return at most 20 series and approximately 200 time buckets per series; terms ranking is by observation frequency, not metric value. Terms aggregation counts can be approximate across shards. Missing split fields are excluded from bucketed series. Individual-event mode returns the latest 1,000 observations and at most 20 series. The response and UI flag incomplete results; narrow the query for a complete view. Percentiles describe submitted observations and are approximate, not pooled percentiles of raw samples hidden inside pre-aggregated benchmark means.

Chart configuration is a typed `chart` property on saved views. It is included in create, update, copy, predefined synchronization, and import/export. Omitting it preserves existing behavior; explicitly patching it to `null` restores the default event/stack count chart. Views without a chart retain their legacy predefined content hashes. Unsaved chart changes are carried in the `chart` URL parameter, including an explicit `null` override; URLs support refresh, sharing, and Back/Forward navigation. Other saved-view display drafts keep their existing storage behavior.

The authenticated API provides:

- `GET /api/v2/organizations/{organizationId}/events/measurements` with optional `filter`, `time`, and `offset`: bounded measurement-name/unit suggestions.
- `POST /api/v2/organizations/{organizationId}/events/chart` with `{ "chart": { "measurement": "duration", "unit": "ms", "aggregation": "avg", "group_by": "source", "mode": "buckets", "display": "line" }, "filter": "type:operation", "time": "[now-7d TO now]" }`: observations or aggregated series.

These APIs enforce organization access, retention, suspension, and premium-search policies. They use repository queries and the existing event store.

## Storage and rollout

Elasticsearch uses a fixed `nested` mapping for `measurements` (`name` and `unit` keywords, `value` double), a `flattened` mapping for labels, and keyword mappings for outcome, result, and relationships. New names do not create new mappings. Each measurement does create an additional hidden nested document, so the per-event limit bounds storage and indexing overhead. Daily index creation includes these mappings; migration 10 adds them to retained partitions. It does not backfill historical custom data or move it into native fields. Deploy the mapping migration before enabling producers.

CI/benchmark reporters, SDK convenience methods, multiple dashboard widgets, baseline comparisons, automatic flaky-test classification, and performance regression alerts are follow-up work. For tests, report passing and failing attempts, retries, and the logical execution identity; failure-only events cannot establish a flakiness rate. For benchmarks, use stable scenario/runtime/machine labels and compare equivalent environments. Keep suite totals separate from child observations in queries to avoid double-counting. Production enablement should measure actual ingest volume, nested-document/storage cost, event quotas, and retention before broad reporting.
