---
name: foundatio
description: >
    Use this skill when working with Foundatio infrastructure abstractions — caching, queuing,
    messaging, file storage, locking, or background jobs. Apply when using ICacheClient, IQueue,
    IMessageBus, IFileStorage, ILockProvider, or IJob, or when implementing retry/resilience
    patterns. Covers both in-memory and production (Redis, Elasticsearch) implementations.
---

# Foundatio

Foundatio provides pluggable infrastructure abstractions. Use context7 MCP for complete documentation.

> **Documentation:** Use `context7` to fetch current Foundatio API docs and examples.

## Core Abstractions

| Interface                   | Purpose               | In-Memory              | Production  |
| --------------------------- | --------------------- | ---------------------- | ----------- |
| `ICacheClient`              | Distributed caching   | `InMemoryCacheClient`  | Redis       |
| `IQueue<T>`                 | Message queuing       | `InMemoryQueue<T>`     | Redis/SQS   |
| `IMessageBus`               | Pub/sub messaging     | `InMemoryMessageBus`   | Redis       |
| `IFileStorage`              | File storage          | `InMemoryFileStorage`  | S3/Azure    |
| `ILockProvider`             | Distributed locking   | `InMemoryLockProvider` | Redis       |
| `IResiliencePolicyProvider` | Retry/circuit breaker | N/A                    | Polly-based |

## ICacheClient

```csharp
var result = await _cache.GetAsync<int>(key);
int value = result.HasValue ? result.Value : 0;

await _cache.IncrementAsync(key, count);
await _cache.ListAddAsync(setKey, organizationId);
```

## IQueue\<T\>

```csharp
await _queue.EnqueueAsync(new EventPost { OrganizationId = orgId, ProjectId = projectId, FilePath = path });
```

## IMessageBus

```csharp
// Subscribe
await _subscriber.SubscribeAsync<EntityChanged>(OnEntityChangedAsync, shutdownToken);

// Publish
await _messagePublisher.PublishAsync(new EntityChanged
{
    ChangeType = ChangeType.Saved, Type = nameof(Organization), Id = organization.Id
});
```

## Jobs

### QueueJobBase — Queue Processing

```csharp
[Job(Description = "Processes queued events.", InitialDelay = "2s")]
public class EventPostsJob : QueueJobBase<EventPost>
{
    public EventPostsJob(IQueue<EventPost> queue, TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider, ILoggerFactory loggerFactory)
        : base(queue, timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        AutoComplete = false;
    }

    protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<EventPost> context)
    {
        var entry = context.QueueEntry;
        // Process the event...
        await entry.CompleteAsync();
        return JobResult.Success;
    }
}
```

### IJob — Scheduled Jobs

```csharp
[Job(Description = "Deletes old data.", InitialDelay = "1m", Interval = "1h")]
public class CleanupDataJob : IJob
{
    public async Task<JobResult> RunAsync(CancellationToken cancellationToken = default) { ... }
}
```

### Job Attributes

| Attribute      | Purpose                  | Example    |
|---------------|--------------------------|------------|
| `Description` | Job description          | —          |
| `InitialDelay`| Delay before first run   | `"2s"`     |
| `Interval`    | Run frequency            | `"5m"`     |
| `IterationLimit` | Run N times then stop | `1`        |
| `IsContinuous`| Keep running             | `true`     |

## Resilience & Reliability

- **Expect failures**: Network calls fail, resources exhaust, concurrent access races
- **Timeouts everywhere**: Never wait indefinitely; use cancellation tokens
- **Retry with backoff**: Use exponential backoff with jitter for transient failures
- **Graceful degradation**: Return cached data, default values, or partial results when appropriate
- **Idempotency**: Design operations to be safely retryable
- **Resource limits**: Bound queues, caches, and buffers to prevent memory exhaustion

Queue entries: `AbandonAsync()` to retry, `CompleteAsync()` to finish. Don't retry validation errors — they'll never succeed.
