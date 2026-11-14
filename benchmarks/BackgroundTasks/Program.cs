using System.Diagnostics;
using System.Text.Json;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Services.BackgroundTasks;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories.Utility;
using Foundatio.Resilience;
using Microsoft.Extensions.Logging.Abstractions;

// A manual service microbenchmark, deliberately outside the correctness test suite.
// Compare the same command before and after a change. This does not measure Redis network latency.
string label = args.FirstOrDefault() ?? "current";
int[] sizes = args.Length > 1 ? args.Skip(1).Select(Int32.Parse).ToArray() : [100, 1_000, 10_000, 50_001];
foreach (int historyCount in sizes)
{
    using var cache = new InMemoryCacheClient(new InMemoryCacheClientOptions { CloneValues = true, MaxItems = null });
    using var messageBus = new InMemoryMessageBus();
    using var queue = new InMemoryQueue<WorkItemData>();
    var measuredCache = new MeasuredCacheClient(cache);
    var locks = new CacheLockProvider(cache, messageBus);
    var tasks = new BackgroundTaskService(measuredCache, locks, messageBus, queue,
        new ResiliencePolicyProvider(), new BackgroundTaskOptions(), TimeProvider.System, NullLogger<BackgroundTaskService>.Instance);

    const string organizationId = "537650f3b77efe23a47914f4";
    var createdUtc = DateTime.UtcNow.AddSeconds(-historyCount - 2);
    var ids = new List<string>(historyCount + 2);
    for (int i = 0; i < historyCount + 2; i++)
    {
        var task = new BackgroundTask
        {
            Id = ObjectId.GenerateNewId(createdUtc.AddSeconds(i)).ToString(),
            OrganizationId = organizationId,
            Type = "benchmark.work",
            Description = "Benchmark task",
            CreatedByUserId = "537650f3b77efe23a47914f5",
            CreatedUtc = createdUtc.AddSeconds(i),
            UpdatedUtc = createdUtc.AddSeconds(i),
            Status = i < 2 ? BackgroundTaskStatus.Running : BackgroundTaskStatus.Completed,
            Progress = i < 2 ? 10 : 100
        };
        ids.Add(task.Id);
        await cache.SetAsync(BackgroundTaskService.GetTaskCacheKey(task.Id), task, BackgroundTaskService.TaskLifetime);
    }

    // Seed equivalent persisted state directly so index construction is outside the measurement.
    await cache.ListAddAsync(BackgroundTaskService.GetOrganizationIndexCacheKey(organizationId), ids, BackgroundTaskService.TaskLifetime);
    await cache.ListAddAsync(BackgroundTaskService.GetActiveIndexCacheKey(organizationId), ids.Take(2), BackgroundTaskService.TaskLifetime);
    async Task<bool> RunAsync(string operation)
    {
        if (operation == "list")
        {
            var result = await tasks.GetByOrganizationAsync(organizationId);
            return result.Count == Math.Min(historyCount, BackgroundTaskService.ListLimit) + 2
                && result[0].Id == ids[^1]
                && result.Count(t => t.IsActive) == 2;
        }

        var updated = await tasks.UpdateAsync(ids[0], task =>
        {
            task.Progress = 20;
            return true;
        });
        return updated is { Progress: 20, IsActive: true };
    }

    foreach (string operation in new[] { "list", "progress" })
    {
        for (int i = 0; i < 3; i++)
            await RunAsync(operation);

        int iterations = historyCount >= 10_000 ? 7 : 21;
        var milliseconds = new List<double>(iterations);
        var allocatedBytes = new List<long>(iterations);
        bool correct = true;
        for (int i = 0; i < iterations; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            measuredCache.Reset();
            long beforeAllocation = GC.GetTotalAllocatedBytes(precise: true);
            long started = Stopwatch.GetTimestamp();
            correct &= await RunAsync(operation);
            milliseconds.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            allocatedBytes.Add(GC.GetTotalAllocatedBytes(precise: true) - beforeAllocation);
        }

        milliseconds.Sort();
        allocatedBytes.Sort();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            label,
            operation,
            historyCount,
            iterations,
            medianMilliseconds = milliseconds[iterations / 2],
            medianAllocatedBytes = allocatedBytes[iterations / 2],
            measuredCache.ListReads,
            measuredCache.SingleReads,
            measuredCache.BatchReads,
            measuredCache.ListWrites,
            measuredCache.ExpirationWrites,
            correct
        }));
    }
}

// Instrument calls while preserving the real cache implementation and its cloning behavior.
sealed class MeasuredCacheClient(ICacheClient inner) : ScopedCacheClient(inner, null), ICacheClient
{
    public int ListReads { get; private set; }
    public int SingleReads { get; private set; }
    public int BatchReads { get; private set; }
    public int ListWrites { get; private set; }
    public int ExpirationWrites { get; private set; }

    public void Reset() => ListReads = SingleReads = BatchReads = ListWrites = ExpirationWrites = 0;

    Task<CacheValue<T>> ICacheClient.GetAsync<T>(string key)
    {
        SingleReads++;
        return UnscopedCache.GetAsync<T>(key);
    }

    Task<IDictionary<string, CacheValue<T>>> ICacheClient.GetAllAsync<T>(IEnumerable<string> keys)
    {
        BatchReads++;
        return UnscopedCache.GetAllAsync<T>(keys);
    }

    Task<CacheValue<ICollection<T>>> ICacheClient.GetListAsync<T>(string key, int? page, int pageSize)
    {
        ListReads++;
        return UnscopedCache.GetListAsync<T>(key, page, pageSize);
    }

    Task<long> ICacheClient.ListAddAsync<T>(string key, IEnumerable<T> values, TimeSpan? expiresIn)
    {
        ListWrites++;
        return UnscopedCache.ListAddAsync(key, values, expiresIn);
    }

    Task ICacheClient.SetExpirationAsync(string key, TimeSpan expiresIn)
    {
        ExpirationWrites++;
        return UnscopedCache.SetExpirationAsync(key, expiresIn);
    }
}
