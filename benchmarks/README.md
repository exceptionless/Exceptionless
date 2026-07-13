# Event ingestion benchmarks

`Exceptionless.Benchmarks` contains allocation-aware microbenchmarks for the V2
parser core (UTF-8 bytes to string, JSON-shape detection, and
`PersistentEvent`/`PersistentEvent[]` deserialization) and the exact production
V3 `EventIngestionV3StreamReader`. The V3 results separately measure bounded
NDJSON framing plus the routing-only projection used by early discard, and the
same path followed by full DTO materialization for survivors. These parsing
microbenchmarks do not include HTTP, decompression, validation, routing I/O,
stack creation, persistence, queues, or side effects; they cannot support an
end-to-end throughput or cost claim by themselves.

Run a short comparison with:

```bash
dotnet run -c Release --project benchmarks/Exceptionless.Benchmarks -- --job short
```

## Apples-to-apples V2 and V3 load comparison

`Exceptionless.Ingestion.Load` submits the same conceptual event corpus through
the real V2 and V3 HTTP endpoints. It records four boundaries:

- **Submission** starts before the first request and stops when the final
  successful response headers arrive. V2's response is only a `202 Accepted`;
  V3's response contains terminal primary-write counts.
- **Observed full processing** is the common semantic terminal boundary, but its
  benchmark instrumentation is intentionally visible in every result. V2
  requests opt into tracking, capture the queue-entry correlation id returned in
  `X-Exceptionless-Event-Post-Id`, and follow any per-event child retries through
  the legacy pipeline and final queue/archive cleanup. V3 polls the existing
  final `notifications` side-effect marker for every persisted survivor, after
  archive, enrichment, statistics, and notification/webhook enqueueing have
  succeeded. A 100%-discard V3 run has no survivor side effects, so its durable
  acknowledgement is also its full processing boundary.
- **V3 durable acknowledgement** is V3-only. It is the successful response after
  the primary event and durable side-effect intent are written. V2 has no
  equivalent client-visible durable-event acknowledgement.
- **Query visibility** uses the same project event-count query and run tag for
  both protocols. It is reported as persisted survivors per second and includes
  the read/index visibility delay. It is `n/a` for a 100%-discard corpus because
  there are intentionally no documents to query.

The V2 request path returns the existing enqueue id and performs no synchronous
tracking-cache I/O, so the submission boundary does not include a tracking-cache
write. Its worker does perform opt-in Redis tracking writes while processing.
V3 adds no completion writes: the status endpoint reads the final idempotency
markers that normal side-effect execution already records. Thus the two
full-processing observers establish the same terminal meaning but do not have
equal instrumentation cost: V2 tracks one identifier per HTTP request while V3
tracks one identifier per persisted event. The harness limits status polling to
`--completion-poll-concurrency` (default 4), removes completed 1,000-identifier
chunks from later sweeps, and records tracked identifiers, status requests,
identifier reads, and sweeps in every trial. A V3 result with many events per
request can still have substantially more observer work and observation lag than
V2; do not attribute an observed-full-processing difference to pipeline
efficiency without these counters and server telemetry.

The completion endpoints are benchmark-oriented, require
`EventIngestionV3:EnableProcessingStatus=true`, and markers expire with
`EventIngestionV3:IdempotencyWindow`, so polling must occur as part of the run.
Submission, observed full processing, and query visibility are reported on the
same corpus. Submission and query visibility have the closest apples-to-apples
observers. Observed full processing is a useful terminal semantic check with the
instrumentation caveat above; V3 durable acknowledgement is reported separately
because V2 has no equivalent contract.

Run separate one-event and large-batch scenarios. `--batch-size 1` sends one JSON
object per V2 request and one NDJSON line per V3 request. A larger batch sends
one V2 JSON array or a V3 stream with exactly one object per NDJSON line. The
load client writes both forms incrementally and does not construct a giant
in-memory array.

Each generated event has a unique `reference_id`, matching production duplicate
detection semantics. A unique tag shared by the run is used only to poll durable
query visibility.

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
only to poll the project count endpoint and is never printed.

The default warmup sends 100 events through each selected protocol and waits for
observed full processing before measurement; odd trials reverse the protocol
order. `--stack-scenario hot` uses stable, protocol-specific signature
namespaces for warmup and every trial.
`--stack-scenario new` uses a fresh namespace for each protocol and trial while
still allowing a separate warmup namespace to warm JIT, HTTP, and datastore
paths. This prevents one protocol from creating or warming the other protocol's
measured stacks. Use `--event-type log` to isolate general pipeline overhead and
`--event-type error` to exercise real stack processing. `none` and `gzip` are
the encodings common to both APIs and therefore the only comparison choices.

Discard comparisons require `--stack-scenario hot`. First run both protocols
with the intended seed and discard percentage but override
`--expected-persisted` to the full event count, then mark the generated
`Load.<seed><protocol>hot.DiscardedException*` stacks discarded. Run the measured
comparison with the same seed and omit `--expected-persisted`; the harness
derives it from `--discard-percent`. V3 reports terminal outcome counts, while
the observed-full-processing boundary proves that discard-only requests finished
without waiting for markers that intentionally do not exist for discarded
events.

Every result includes request count, payload bytes, common submission and
observed-full-processing events/second, query-visible persisted events/second,
request latency p50/p95/p99, completion-observer counters, query poll count, V3
durable acknowledgement, and V3 terminal counts. Pass
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
