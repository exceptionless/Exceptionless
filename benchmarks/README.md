# Event ingestion benchmarks

`Exceptionless.Benchmarks` contains allocation-aware microbenchmarks that compare
the V2 parser core (UTF-8 bytes to string, JSON-shape detection, and
`PersistentEvent`/`PersistentEvent[]` deserialization) with the exact production
V3 `EventIngestionV3StreamReader`. The V3 payloads are written with the app JSON
options and read as NDJSON and as a JSON array. A second benchmark class repeats
the comparison for a single event with a large stack trace. These
microbenchmarks do not include HTTP, decompression, validation, the event
pipeline, persistence, queues, or side effects; they cannot support an
end-to-end throughput or cost claim by themselves.

Run a short comparison with:

```bash
dotnet run -c Release --project benchmarks/Exceptionless.Benchmarks -- --filter '*' --job short
```

## V2 and V3 load comparison

`Exceptionless.Ingestion.Load` submits the same conceptual event corpus through
the real V2 and V3 HTTP endpoints. It records two boundaries:

- **Submission** starts before the first request and stops when the final
  successful response arrives. Request latency p50/p95/p99 is reported for the
  same requests. V2's response is `202 Accepted` after the post is queued. V3
  processes each microbatch inline through the same event pipeline V2 uses, so
  its `200` response means the events are stored and post-processed. The V3
  response counts (`received`, `persisted`, `discarded`, `duplicate`, `blocked`,
  `invalid`, `failed`) are summed and checked against the expected totals.
- **Query visibility** is the common end-to-end boundary. It uses the same
  project event-count query and run tag for both protocols and is reported as
  persisted events per second and as elapsed time from the start of the run,
  including the queue and read/index visibility delay. It is `n/a` for a
  100%-discard corpus because there are intentionally no documents to query.

Because the submission boundaries differ, compare submission latency only with
that in mind and use query visibility to compare the two protocols end to end.

Run separate one-event and large-batch scenarios. `--batch-size 1` sends one JSON
object per V2 request and one NDJSON line per V3 request
(`Content-Type: application/x-ndjson`). A larger batch sends one V2 JSON array or
a V3 stream with one object per NDJSON line. The load client writes both forms
incrementally and does not construct a giant in-memory array.

Each generated event has a unique `reference_id`, matching production duplicate
detection semantics, and V3 events also send a unique `id`. A unique tag shared
by the run is used to poll query visibility.

```bash
export EXCEPTIONLESS_API_KEY=...
export EXCEPTIONLESS_PROJECT_ID=...
export EXCEPTIONLESS_READ_TOKEN=...

# One event per request.
dotnet run -c Release --project benchmarks/Exceptionless.Ingestion.Load -- \
  --base-url https://api-ex.dev.localhost:7111/ --protocol both \
  --events 10000 --batch-size 1 --concurrency 32 --trials 5 \
  --event-type error --stack-scenario hot --signature-cardinality 100 \
  --compression gzip --seed single-error \
  --results artifacts/ingestion-single-error.json \
  --environment-label "1 API; 1 job; local Elasticsearch and Redis"

# Large real-world client batches.
dotnet run -c Release --project benchmarks/Exceptionless.Ingestion.Load -- \
  --base-url https://api-ex.dev.localhost:7111/ --protocol both \
  --events 100000 --batch-size 1000 --concurrency 8 --trials 5 \
  --event-type error --stack-scenario hot --signature-cardinality 100 \
  --compression gzip --seed batch-error \
  --results artifacts/ingestion-batch-error.json \
  --environment-label "1 API; 1 job; local Elasticsearch and Redis"
```

Instead of `EXCEPTIONLESS_READ_TOKEN`, a local comparison can use
`--read-user <email> --read-password <password>`. The read credential is needed
only to poll the project count endpoint and is never printed. Run with `--help`
to list every option, including `--poll-interval-ms` for the count query.

The default warmup sends 100 events through each selected protocol and waits for
query visibility before measurement; odd trials reverse the protocol order.
`--stack-scenario hot` uses stable, protocol-specific signature namespaces for
warmup and every trial. `--stack-scenario new` uses a fresh namespace for each
protocol and trial while still allowing a separate warmup namespace to warm JIT,
HTTP, and datastore paths. This prevents one protocol from creating or warming
the other protocol's measured stacks. Use `--event-type log` to isolate general
pipeline overhead and `--event-type error` to exercise real stack processing.
`none` and `gzip` are the encodings common to both APIs and therefore the only
comparison choices.

Discard comparisons require `--stack-scenario hot`. First run both protocols
with the intended seed and discard percentage but override
`--expected-persisted` to the full event count, then mark the generated
`Load.<seed><protocol>hot.DiscardedException*` stacks discarded. Run the measured
comparison with the same seed and omit `--expected-persisted`; the harness
derives it from `--discard-percent`, and V3 must report that many persisted
events.

Every result includes request count, payload bytes, submission events/second,
request latency p50/p95/p99, query-visible persisted events/second, the number of
count-query polls, and the V3 response counts. Pass
`--results artifacts/ingestion-v3.json` to write a machine-readable, secret-free
artifact containing every trial, sanitized configuration, runtime/OS/CPU
metadata, and the build informational version. Use `--environment-label` for
the Elasticsearch/Redis topology and API/job instance counts. Publish the JSON
artifact with the exact commit and do not report a comparison from warmup output
or from a single trial.

The load client records payload bytes and client-observed timings, not server
CPU, allocation rate, GC collections, Redis/Elasticsearch operation counts, or
cost. Collect those from the API, job, Redis, and Elasticsearch telemetry for
the same run before making efficiency or cost-per-million claims.
