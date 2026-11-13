# Event ingestion load baseline

`Exceptionless.Ingestion.Load` posts generated events to the V2 event API and records how long the current implementation takes to accept them and to make them queryable. Use it to capture a baseline before changing the ingestion path and to repeat the same scenarios afterwards.

It targets `POST /api/v2/events` by default, or `POST /api/v2/projects/{project-id}/events` with `--route project`. It is a measurement tool with no correctness assertions, so it is not part of `dotnet test`.

## Start the local stack

From the repository root:

```bash
aspire run
```

Wait until the `Api` and `Job` resources are healthy. The API accepts and queues each post, and the `Job` resource processes the queue and stores the events, so both are needed for query visibility to complete.

Ports can be dynamic, so do not assume one. Read the API endpoint the AppHost emitted from the dashboard, or from the `Api` resource in:

```bash
aspire describe --apphost src/Exceptionless.AppHost --format Json --non-interactive
```

Use that endpoint as `--base-url`. If the local development certificate is not trusted by .NET, use the `Api` resource's `http` endpoint instead. Run the harness against the local stack only.

## Credentials

The harness needs a project and two credentials, which it never prints or writes to the evidence file:

- `--project-id` (or `EXCEPTIONLESS_PROJECT_ID`) is the project that receives the events and whose event count is queried.
- `--submission-token` (or `EXCEPTIONLESS_API_KEY`) is that project's API key.
- A read credential is only used to poll the event count: `--read-token` (or `EXCEPTIONLESS_READ_TOKEN`), or `--read-user` and `--read-password` (or `EXCEPTIONLESS_READ_USER` and `EXCEPTIONLESS_READ_PASSWORD`).

In `Development` mode the AppHost seeds a sample project with a client API key, and the administrator user described in the root [README](../README.md) can read events. `tests/http/events.http` lists the sample project ID and API key. Prefer environment variables over command-line options so credentials stay out of shell history.

## What is measured

Each trial posts `--events` events and reports:

- **Request latency p50/p95/p99** across the trial's requests, from sending a request until its response headers arrive.
- **Submission events/s**, from the start of the trial until the final successful response arrives. Any 2xx response is a success; V2 returns `202 Accepted` once the post is queued, before any event is stored. Any other status fails the run.
- **Query visibility**, the elapsed time from the start of the trial until `GET /api/v2/projects/{project-id}/events/count` filtered by the trial's unique tag reports every expected persisted event, plus the matching persisted events per second. It includes queueing, job processing, and index refresh, so subtracting the submission time gives the delay after acceptance. It is `n/a` when no events are expected to persist.

The console prints one line per trial and the median of the measured trials. Pass `--results artifacts/<name>.json` to also write a machine-readable file with every trial, the sanitized configuration, runtime, operating system, and processor metadata, and the build version. Use `--environment-label` for the topology, such as the number of API and job instances and where Elasticsearch and Redis run. Keep the file with the exact commit that produced it, and do not report numbers from warmup output or from a single trial.

The harness records client-observed timings and payload bytes. It does not capture server CPU, allocations, garbage collections, Redis or Elasticsearch operation counts, or cost; collect those from the API, job, Redis, and Elasticsearch telemetry for the same run when a claim needs them.

## Scenarios

Run `--help` to list every option. Each event has a unique `reference_id` and carries a tag unique to the trial that is used for the count query. Requests are streamed without a known content length.

| Scenario | Options |
| --- | --- |
| One event per request | `--batch-size 1` posts one JSON object per request. |
| JSON array batch of N events | `--batch-size N` posts one JSON array of up to N events per request. |
| Gzip off or on | `--compression none` (default) or `--compression gzip` sets `Content-Encoding`. |
| Client concurrency | `--concurrency C` keeps up to C requests in flight. |
| Volume and repeats | `--events`, `--trials`, and `--warmup-events` (default 100; the warmup waits for query visibility, and `0` disables it). |
| Event shape | `--event-type log` isolates general pipeline overhead; `--event-type error` (default) exercises stack processing. |
| Stack reuse | `--stack-scenario hot` (default) reuses `--signature-cardinality` stack signatures for the warmup and every trial; `new` uses fresh signatures for each trial. |
| Discard ratio | `--discard-percent P`, described below. |

Example baseline for single events and for large batches, with and without gzip:

```bash
export EXCEPTIONLESS_PROJECT_ID=...
export EXCEPTIONLESS_API_KEY=...
export EXCEPTIONLESS_READ_USER=...
export EXCEPTIONLESS_READ_PASSWORD=...
API_URL=...   # the Api endpoint from aspire describe

for compression in none gzip; do
  # One event per request.
  dotnet run -c Release --project benchmarks/Exceptionless.Ingestion.Load -- \
    --base-url "$API_URL" --events 10000 --batch-size 1 --concurrency 32 --trials 5 \
    --compression "$compression" --seed "single-$compression" \
    --results "artifacts/ingestion-single-$compression.json" \
    --environment-label "1 API; 1 job; local Elasticsearch and Redis"

  # JSON array batches of 1000 events.
  dotnet run -c Release --project benchmarks/Exceptionless.Ingestion.Load -- \
    --base-url "$API_URL" --events 100000 --batch-size 1000 --concurrency 8 --trials 5 \
    --compression "$compression" --seed "batch-$compression" \
    --results "artifacts/ingestion-batch-$compression.json" \
    --environment-label "1 API; 1 job; local Elasticsearch and Redis"
done
```

Use `--route project` to measure the explicit-project route instead of `POST /api/v2/events`.

### Discard ratio

A discard scenario needs stacks that are already marked discarded. Generated events use the exception type `Load.<seed without hyphens>hot.ActiveException<N>` or `Load.<seed without hyphens>hot.DiscardedException<N>`, and `--discard-percent P` makes the first P of every 100 events use a discarded signature.

1. Run once with `--event-type error --stack-scenario hot --discard-percent P --expected-persisted <events>` so every event is stored and the stacks are created.
2. Mark the `Load.<seed without hyphens>hot.DiscardedException*` stacks discarded, for example with `POST /api/v2/stacks/{ids}/change-status?status=discarded`.
3. Repeat the run with the same `--seed` and `--discard-percent` and without `--expected-persisted`. The harness expects only the non-discarded events to become visible.

## Interpreting results

Numbers from a development machine are indicative only. The harness, API, job, Elasticsearch, Redis, and the Aspire dashboard share the same machine, so other processes, thermal limits, and local data volume affect the results. Compare runs only on the same machine with the same configuration, and when cost or capacity claims are needed, repeat the scenarios on dedicated infrastructure.

An organization with a monthly event limit does not store events above the limit, so the count never reaches the expected total and the run ends at `--timeout-seconds`. The seeded development organization uses the unlimited plan.
