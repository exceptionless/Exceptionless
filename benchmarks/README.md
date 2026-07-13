# Event ingestion benchmarks

`Exceptionless.Benchmarks` contains allocation-aware microbenchmarks for V2-style
array deserialization versus V3 top-level-value streaming, raw stack
fingerprinting, complete server-side stack parsing, and survivor materialization.

Run a short comparison with:

```bash
dotnet run -c Release --project benchmarks/Exceptionless.Benchmarks -- --job short
```

## Apples-to-apples V2 and V3 load comparison

`Exceptionless.Ingestion.Load` submits the same conceptual event corpus through
the real V2 and V3 HTTP endpoints. It records two different boundaries:

- **Submission** starts before the first request and stops after the last HTTP
  response. V2's response is only a `202 Accepted`; V3's response contains
  terminal processing counts.
- **Processed** starts at the same point. V2 stops when the expected queued
  events are query-visible through the events count API, including payload
  storage, queue delay, background job processing, persistence, and index
  refresh. V3 stops at its terminal `200` response, whose contract requires
  persistence before acknowledgement; the runner verifies its persisted count.
  It deliberately does not add the normal read API's stack-visibility cache
  delay to V3 processing time.

Run separate one-event and large-batch scenarios. `--batch-size 1` sends one JSON
object per V2 request and one top-level value per V3 request. A larger batch sends
one V2 JSON array or a V3 stream of top-level JSON values per request. The load
client writes both forms incrementally and does not construct a giant in-memory
array.

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
  --event-type error --signature-cardinality 100 --compression gzip \
  --seed single-error

# Large real-world client batches.
dotnet run -c Release --project benchmarks/Exceptionless.Ingestion.Load -- \
  --base-url https://api-ex.dev.localhost:7111/ --protocol both \
  --events 100000 --batch-size 1000 --concurrency 8 --trials 5 \
  --event-type error --signature-cardinality 100 --compression gzip \
  --seed batch-error
```

Instead of `EXCEPTIONLESS_READ_TOKEN`, a local comparison can use
`--read-user <email> --read-password <password>`. The read credential is needed
only to poll the project count endpoint and is never printed.

The default warmup sends 100 events through each selected protocol before
measurement, and odd trials reverse the protocol order. Set `--warmup-events 0`
for an intentional cold-stack test. Use `--event-type log` to isolate general
pipeline overhead and `--event-type error` to exercise each API's real stack
processing behavior. `none` and `gzip` are the encodings supported by both
protocols and therefore the only comparison choices. Error stack names include
the sanitized `--seed`; use a fresh seed for an isolated corpus and the same seed
for every run that should reuse its warmed stacks.

For a discarded-stack run, first create and discard the corresponding
`Load.DiscardedException*` stacks, set `--discard-percent`, and set
`--expected-persisted` to the number that should remain query-visible. V3's
terminal discarded count is reported; V2 has no equivalent terminal response.

Every result includes request count, payload bytes, submission and processed
events/second, the post-ack drain time, request latency p50/p95/p99, and V3
terminal counts. Record the commit, SDK/runtime, machine, Elasticsearch and
Redis topology, API and job instance counts, compression, concurrency, event
size, signature cardinality, and complete command line with published results.
