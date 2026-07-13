# Event ingestion benchmarks

`Exceptionless.Benchmarks` contains allocation-aware microbenchmarks for V2-style
array deserialization versus V3 top-level-value streaming, raw stack
fingerprinting, complete server-side stack parsing, and survivor materialization.

Run a short comparison with:

```bash
dotnet run -c Release --project benchmarks/Exceptionless.Benchmarks -- --job short
```

`Exceptionless.Ingestion.Load` is the end-to-end reference streaming client and
load harness. It writes each event directly to the request stream, never builds
a top-level array, uses chunked HTTP when HTTP/1.1 is negotiated, and supports
gzip or Brotli streaming compression.

```bash
EXCEPTIONLESS_API_KEY=... dotnet run -c Release \
  --project benchmarks/Exceptionless.Ingestion.Load -- \
  --url https://localhost:7131/api/v3/events \
  --events 100000 --concurrency 8 --segment-size 100 \
  --signature-cardinality 100 --discard-percent 50 --compression br \
  --seed mixed-50
```

Event identifiers are deterministic from `--seed` and event index. Repeating a
run exercises replay idempotency. To measure discarded traffic, first create the
candidate stacks with a fixed seed/signature cardinality, mark the desired
`Load.DiscardedException*` stacks discarded, then repeat the same run. The
harness reports terminal response counts and does not print the API key.

For comparable results, record the commit, SDK/runtime, machine, Elasticsearch
and Redis topology, API instance count, compression, concurrency, event size,
signature cardinality, discard percentage, and command line with every result.
