# Background task service benchmark

This manual executable measures task listing and active progress updates using the real `BackgroundTaskService` and Foundatio in-memory cache. It is outside the normal test suite and solution. It adds no benchmark package or production dependency.

## Run

From the repository root with the .NET 11 SDK:

```sh
dotnet run --project benchmarks/BackgroundTasks/BackgroundTasks.csproj --configuration Release -- current
```

The first argument labels the results. Optional following arguments replace the finished-history sizes:

```sh
dotnet run --project benchmarks/BackgroundTasks/BackgroundTasks.csproj --configuration Release -- current 100 1000 10000 50001
```

To compare revisions, create a separate worktree at the baseline, copy this folder's `Program.cs` and `.csproj` into the same location there, and run the same command in each checkout. Keep the SDK, configuration, dependencies, and machine identical. Run the measurements sequentially without simultaneous builds or test suites.

## What is measured

- Seed each history size plus two older active tasks. Setup is outside the measurement. Cache values are cloned as in application use; capacity eviction is disabled so it does not change the selected workload.
- Warm each operation three times, then take 21 samples for smaller histories or seven for histories of 10,000 or more.
- Report median elapsed time and total managed allocation per call, including asynchronous work. Force collection outside each sample to make retained setup state comparable.
- Count service-level index reads, individual and batched record reads, index renewals, and expiry updates. The measuring decorator delegates directly to the underlying cache.
- Check that listing returns the newest finished task, the expected count, and both active tasks. Check that progress updates retain the running task and requested progress.

Allocation means bytes allocated during an operation, not retained process memory. This is a service microbenchmark, not an Elasticsearch throughput test or a Redis latency forecast. `GetAllAsync` enables batching, but provider behavior and Redis Cluster hash-slot placement determine actual network requests. Progress measurements use two active entries; costs for a larger active index must be measured separately.

## Recorded comparison

Measured on Linux x64, .NET SDK 11.0.100, Release, Foundatio 13.0.5. The baseline is [`fc804c08`](https://github.com/exceptionless/Exceptionless/commit/fc804c084506f5f78fc80b11209b49e437423587). The candidate is the service implementation accompanying this report. [comparison.json](comparison.json) contains every recorded sample summary and the candidate service blob identifier.

| Finished history | Baseline listing | Candidate listing | Baseline allocation | Candidate allocation |
| ---: | ---: | ---: | ---: | ---: |
| 100 | 0.1340 ms | 0.1573 ms | 0.050 MB | 0.061 MB |
| 1,000 | 0.9845 ms | 0.6104 ms | 0.308 MB | 0.188 MB |
| 10,000 | 16.6519 ms | 5.2518 ms | 10.578 MB | 1.364 MB |
| 50,001 | 152.1701 ms | 17.0455 ms | 233.967 MB | 6.241 MB |

The 50,001 baseline result is **incorrect**: the old page cap omits the newest task. Its timing is included to expose the old work and failure, not as a comparison between two correct implementations. All candidate result checks passed.

At 10,000 finished tasks, both versions return the correct result; the candidate used about 87% fewer allocated bytes and 68% less elapsed time. The candidate performs two index reads and two batched record reads for these fixtures. The baseline performs 22 index reads and 52 individual record reads at 10,000 tasks, rising to 101 index reads at 50,001.

The small-history tradeoff is visible: at 100 tasks, dictionary batching adds about 12 KB of allocation and 0.023 ms of in-process work. It reduces sequential cache calls, which this local benchmark does not price as remote latency. Avoid adding provider-specific branches or caching layers to optimize that small absolute cost without a representative workload.

### Retention cost

Progress updates now renew the small active index and its key expiry so a task remains discoverable for the lifetime of its saved record. They do not rewrite the history index. With two active tasks, median allocation increased from 6,640 to 9,128 bytes at the first three history sizes: about 2.5 KB per update. At 1,000 history entries, the observed median was 0.0773 ms before and 0.1047 ms after. Submillisecond timings vary with runtime and host scheduling; the extra renewal and expiry calls are the stable change.

Terminal updates renew history before removing active membership. These measurements cover active progress, not terminal-history writes. Index retention is a correctness obligation; report its write cost alongside read improvements instead of claiming that every operation became faster.

## Remaining scaling boundary

The portable cache API has no ordered recent-history query. Listing still reads and sorts all live history IDs, and returning all active tasks requires materializing that active set. The improvement removes repeated index copies, the page cutoff, and sequential record reads. A future store with ordered discovery can change that access pattern when real volume warrants it; the [task guide](../../docs/background-tasks.md) describes that extraction boundary.
